# Идеальный День

Планировщик дня с учётом биоритмов и хронотипа. Сервер: ASP.NET Core (.NET 10), EF Core, PostgreSQL. Клиент: React, TypeScript, Vite.

## Запуск

Нужны .NET 10 SDK, Node.js 22 или новее и Docker.

```bash
docker compose up -d
dotnet run --project PerfectDay_server/PerfectDay_API --launch-profile http
```

Во втором терминале:

```bash
cd PerfectDay_client
npm install
npm run dev
```

Приложение откроется на http://localhost:5173. В режиме Development сервер сам применяет миграции к базе.

## Тесты

```bash
dotnet test PerfectDay_server/PerfectDay_Tests
cd PerfectDay_client && npm test
```

## Настройки для запуска вне разработки

В `appsettings.Development.json` лежат только локальные значения. В других окружениях задайте переменные `ConnectionStrings__Default` и `Jwt__Key` (ключ не короче 32 байт) и примените миграции командой `dotnet ef database update`.
