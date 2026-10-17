# Docker Lab Log

## Part 0 - Environment setup
Output of `docker compose version`:

```
Docker Compose version v5.4.0
```

## Part 1 - Run a prebuilt container
- Image: `nginx`
- Container name: `web`
- Port mapping: `8081:80` (host port 8081 -> container port 80)

## Part 2 - Build an image for the API
- Image: `myapi:1.0`, 335MB disk usage (content size 93.9MB), far smaller than the `sdk:8.0` base image.
- Kestrel startup line from `docker logs myapi`:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:8080
```

- `/api/health` response: `{"status":"healthy","host":"c8622f88c253"}`

## Part 3 - Database with Docker Compose

`docker compose ps` (both services running, db healthy):

```
NAME          IMAGE                                        COMMAND                  SERVICE   CREATED          STATUS                    PORTS
myapi-api-1   myapi-api                                    "dotnet MyApi.dll"       api       18 seconds ago   Up 10 seconds             0.0.0.0:8080->8080/tcp, [::]:8080->8080/tcp
myapi-db-1    mcr.microsoft.com/mssql/server:2022-latest   "/opt/mssql/bin/laun..."   db        18 seconds ago   Up 18 seconds (healthy)   1433/tcp
```

`/api/items` response:

```
[{"id":1,"name":"Keyboard"},{"id":2,"name":"Mouse"},{"id":3,"name":"Monitor"}]
```

Persistence: I ran `docker compose down` (without `-v`) and then `docker compose up -d`. The data survived because it is stored in the named volume `sqldata`.

## Part 4 - Full multi-service stack, scaled to 3 API replicas

`docker compose ps`:

```
NAME            IMAGE                                        COMMAND                  SERVICE   CREATED          STATUS                   PORTS
myapi-api-1     myapi-api                                    "dotnet MyApi.dll"       api       35 seconds ago   Up 34 seconds            8080/tcp
myapi-api-2     myapi-api                                    "dotnet MyApi.dll"       api       35 seconds ago   Up 34 seconds            8080/tcp
myapi-api-3     myapi-api                                    "dotnet MyApi.dll"       api       35 seconds ago   Up 34 seconds            8080/tcp
myapi-cache-1   redis:7                                      "docker-entrypoint.s..."   cache     35 seconds ago   Up 35 seconds            6379/tcp
myapi-db-1      mcr.microsoft.com/mssql/server:2022-latest   "/opt/mssql/bin/laun..."   db        2 minutes ago    Up 2 minutes (healthy)   1433/tcp
myapi-nginx-1   nginx                                        "/docker-entrypoint...."   nginx     35 seconds ago   Up 33 seconds            0.0.0.0:80->80/tcp, [::]:80->80/tcp
```

Load balancing: nine calls to `http://localhost/api/health` rotated across the three replicas (the `host` value is the container ID):

```
{"status":"healthy","host":"7bfee3833c04"}
{"status":"healthy","host":"ff30b958d12a"}
{"status":"healthy","host":"32a4c925b195"}
{"status":"healthy","host":"7bfee3833c04"}
{"status":"healthy","host":"ff30b958d12a"}
{"status":"healthy","host":"32a4c925b195"}
{"status":"healthy","host":"7bfee3833c04"}
{"status":"healthy","host":"ff30b958d12a"}
{"status":"healthy","host":"32a4c925b195"}
```

Only nginx publishes a host port (80). The API replicas, SQL Server, and Redis are reachable only inside the Compose network.

## Reflection

Multi-stage builds let me compile with the full .NET SDK but ship only the small ASP.NET runtime image plus the published output, so the final image is 335MB and contains no compilers or source code. A smaller image pulls faster and has less attack surface. The database health check, combined with `depends_on` using `condition: service_healthy`, stopped the API from starting before SQL Server could accept connections, which removed the startup race and the connection errors that come with it. Together these made the stack reproducible: it comes up the same way every time with one command.
