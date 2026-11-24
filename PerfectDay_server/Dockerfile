# Сборка внутри контейнера: .NET SDK на машине не нужен.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только проекты, чтобы restore кешировался, пока не меняются зависимости.
COPY PerfectDay_Domain/PerfectDay_Domain.csproj PerfectDay_Domain/
COPY PerfectDay_Application/PerfectDay_Application.csproj PerfectDay_Application/
COPY PerfectDay_Infrastructure/PerfectDay_Infrastructure.csproj PerfectDay_Infrastructure/
COPY PerfectDay_API/PerfectDay_API.csproj PerfectDay_API/
RUN dotnet restore PerfectDay_API/PerfectDay_API.csproj

COPY . .
RUN dotnet publish PerfectDay_API/PerfectDay_API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PerfectDay_API.dll"]
