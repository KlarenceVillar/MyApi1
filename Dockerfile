# syntax=docker/dockerfile:1

# ---- Stage 1: build & publish (uses the full SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MyApi.csproj .
RUN dotnet restore MyApi.csproj

COPY . .
RUN dotnet publish MyApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Stage 2: runtime (small ASP.NET image, no SDK) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MyApi.dll"]