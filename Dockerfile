# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + project files first for layer caching
COPY SreTakeHome.sln global.json ./
COPY src/CandidateApi/CandidateApi.csproj src/CandidateApi/
COPY src/CandidateApi.Contracts/CandidateApi.Contracts.csproj src/CandidateApi.Contracts/
COPY tests/CandidateApi.Tests/CandidateApi.Tests.csproj tests/CandidateApi.Tests/
RUN dotnet restore src/CandidateApi/CandidateApi.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish src/CandidateApi/CandidateApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CandidateApi.dll"]
