Учёт баллов ППС (СГУПС)
Внутреннее веб-приложение для учёта индивидуальных показателей работы преподавателей (ИП) по семестрам и формирования отчётов с экспортом в Excel и PDF.

Стек: .NET 10 · Blazor (Interactive Server) · EF Core 10 · SQL Server. Экспорт: ClosedXML (Excel) и QuestPDF (PDF).

Возможности
Выбор преподавателя (с группировкой по кафедрам) и добавление нового (ФИО + кафедра).
Выбор периода: учебный год + семестр.
Добавление работ:
фиксированный балл — подставляется автоматически;
переменный балл (3/5/7, 30/50, 2/4/6/15 и т. п.) — выбор варианта пользователем;
ручной ввод (поручения зав. кафедрой);
количественный (1 балл за 1 практику).
комментарий и прикреплённый файл-подтверждение (до 25 МБ).
Таблица внесённых работ с группировкой по разделам, редактированием и удалением.
Автоматический учёт лимитов: ≤ 50 баллов на раздел, ≤ 100 баллов итого, итог не меньше 0.
Формирование отчёта: выбор конкретных преподавателей / целой кафедры / всех.
Отчёт на сайте + экспорт в Excel и PDF с местом под подпись зав. кафедрой и печать (М.П.).
Требования
.NET SDK 10.0+
SQL Server: подойдёт LocalDB (идёт с Visual Studio), Express или полноценный экземпляр.
Запуск
Открыть PpsScoreApp.sln в Visual Studio 2022/2026 (или Rider), либо использовать CLI.

Восстановить пакеты и собрать:

cd src/PpsScoreApp
dotnet restore
dotnet run
Открыть адрес из консоли (по умолчанию https://localhost:7242).

При первом запуске база данных создаётся автоматически (EnsureCreated) и наполняется справочниками: 36 кафедр и ~70 видов работ по 5 разделам. Пользовательские данные при повторных запусках не затрагиваются.

Строка подключения
src/PpsScoreApp/appsettings.json → ConnectionStrings:DefaultConnection.

По умолчанию используется LocalDB:

Server=(localdb)\MSSQLLocalDB;Database=PpsScoreDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
Для другого сервера замените строку, напр.:

Server=localhost;Database=PpsScoreDb;User Id=sa;Password=ВашПароль;TrustServerCertificate=True
Хранение прикреплённых файлов
Файлы сохраняются в App_Data/uploads (рядом с проектом). Путь можно переопределить в appsettings.json → FileStorage:Path.

Переход на EF-миграции (опционально)
Для быстрого старта схема создаётся через EnsureCreated(). Чтобы перейти на миграции (рекомендуется при дальнейшем развитии схемы):

В Data/DbInitializer.cs убрать строку await db.Database.EnsureCreatedAsync(); и заменить на await db.Database.MigrateAsync();.

Удалить созданную ранее БД (если была создана через EnsureCreated — в ней нет таблицы истории миграций).

Создать и применить миграцию:

dotnet tool install --global dotnet-ef        # если ещё не установлен
cd src/PpsScoreApp
dotnet ef migrations add Initial
dotnet ef database update
Сидирование справочников останется рабочим — оно выполняется в DbInitializer после применения схемы.

Структура проекта
src/PpsScoreApp/
├── Domain/            доменные сущности (Department, Teacher, WorkType, ScoreOption, WorkEntry)
├── Data/              DbContext, сидинг справочников (кафедры, виды работ), инициализатор БД
├── Services/          подсчёт лимитов, построение отчёта, экспорт Excel/PDF, хранение файлов
├── Models/            модели отчёта
├── Components/
│   ├── Pages/         Home (ввод показателей), Report (отчёты), Error
│   └── Layout/        MainLayout
└── wwwroot/           app.css, app.js
Лимиты баллов
Реализованы в Services/ScoringRules.cs: SectionCap = 50, TotalCap = 100, TotalFloor = 0. Итог = сумма разделов (каждый ≤ 50), затем ограничение сверху 100 и снизу 0.

Лицензия QuestPDF
Используется Community-лицензия (бесплатна для организаций с годовой выручкой < $1 млн). Установлена в Program.cs. При необходимости замените на свою.

Замечания
Версии NuGet-пакетов (PpsScoreApp.csproj) при необходимости поднимите до доступных в вашем фиде: Microsoft.EntityFrameworkCore.SqlServer/.Design — под вашу версию .NET 10, ClosedXML и QuestPDF — до последних совместимых.
Шрифт PDF — Times New Roman (присутствует в Windows). На Linux-сервере без этого шрифта QuestPDF выполнит подстановку; при необходимости зарегистрируйте TTF через FontManager.RegisterFont(...).
Приложение без авторизации — рассчитано на внутреннюю сеть.
Справочник видов работ и баллов вынесен в Data/SeedData.WorkTypes.cs — там же можно скорректировать формулировки, баллы и варианты.