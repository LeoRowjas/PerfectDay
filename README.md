# Идеальный День

Планировщик дня с учётом биоритмов и хронотипа. Сервер: ASP.NET Core (.NET 10), EF Core, PostgreSQL. Клиент: React, TypeScript, Vite.

## Запуск без .NET (для фронтенда)

Нужны только Docker и Node.js 22 или новее.

```bash
docker compose up -d --build      # база и API на :5244, первая сборка займёт пару минут
cd PerfectDay_client
npm install
npm run dev
```

Приложение откроется на http://localhost:5173. Сервер сам применяет миграции к базе. После `git pull` с изменениями на сервере снова запустите `docker compose up -d --build`. Логи API: `docker compose logs -f api`, остановить всё: `docker compose down` (данные базы сохранятся).

## Запуск для разработки сервера

Нужны .NET 10 SDK, Node.js 22 или новее и Docker.

```bash
docker compose up -d postgres
dotnet run --project PerfectDay_server/PerfectDay_API --launch-profile http
```

Во втором терминале:

```bash
cd PerfectDay_client
npm install
npm run dev
```

Если API уже запущен в контейнере, сначала остановите его: `docker compose stop api`, иначе порт 5244 занят.

## Тесты

```bash
dotnet test PerfectDay_server/PerfectDay_Tests
cd PerfectDay_client && npm test
```

## Настройки для запуска вне разработки

В `appsettings.Development.json` лежат только локальные значения. В других окружениях задайте переменные `ConnectionStrings__Default` и `Jwt__Key` (ключ не короче 32 байт) и примените миграции командой `dotnet ef database update`.
