# CLAUDE.md — база знаний проекта «Учёт баллов ППС»

Этот файл — контекст для Claude Code. Прочитай его целиком перед изменениями.
Отвечай и комментируй по-русски. Все правки — минимальные и точечные, не ломай
существующую логику.

---

## 1. Назначение

Внутреннее веб-приложение СГУПС для учёта индивидуальных показателей работы (ИП)
профессорско-преподавательского состава по семестрам и формирования отчётности
по кафедрам. Преподаватели/сотрудники кафедр вносят баллы за виды работ, система
считает итоги с лимитами и формирует пакет документов (Excel-форма + пояснения Word
+ приложенные файлы) в ZIP.

Рассчитано на внутреннюю сеть, без внешнего доступа.

---

## 2. Стек и версии (важно, не поднимать бездумно)

- **.NET 9** (`net9.0`). Ранее был .NET 10 — намеренно понижен до 9.
- **Blazor Web App**, рендеринг **Interactive Server**, режим задаётся **по странице**
  (`@rendermode InteractiveServer`), а не глобально.
- **EF Core 9.0.9** (`Microsoft.EntityFrameworkCore.SqlServer` + `.Design`) + **SQL Server**.
- **ClosedXML 0.105.0** — Excel.
- **DocumentFormat.OpenXml 3.5.1** — Word.
- **QuestPDF 2026.6.0** — PDF (лицензия Community, включается в `Program.cs`).

Пакеты, кроме EF Core, к версии .NET не привязаны. EF Core обязан соответствовать
TFM: **не ставить EF Core 10 на net9.0**.

---

## 3. Команды

```bash
cd src/PpsScoreApp
dotnet restore
dotnet run          # https://localhost:7242 (по умолчанию)
dotnet build
```

Строка подключения — `src/PpsScoreApp/appsettings.json` → `ConnectionStrings:DefaultConnection`.
Путь хранения файлов — `FileStorage:Path` (пусто = `App_Data/uploads` рядом с приложением).

---

## 4. КРИТИЧНЫЕ ПРАВИЛА И ПОДВОДНЫЕ КАМНИ

Здесь собраны грабли, на которые уже наступали. Читать обязательно.

### 4.1. База: EnsureCreated, НЕ миграции
`DbInitializer` вызывает `EnsureCreatedAsync()` (см. `Data/DbInitializer.cs`).
Это значит: **изменения схемы НЕ применяются к уже существующей БД автоматически**.
Любая новая таблица/колонка требует ручного SQL (`ALTER`/`CREATE`) на рабочей базе,
либо пересоздания БД. При добавлении сущности:
1. добавить класс в `Domain/`, `DbSet` в `AppDbContext`, конфигурацию связей/индексов;
2. **обязательно дать пользователю SQL `CREATE TABLE`/`ALTER`** для существующей БД;
3. при необходимости — SQL для переноса данных.
Переход на миграции описан в README; пока не сделан.

### 4.2. SQL Server ниже 2016 → OPENJSON
EF Core для `list.Contains(x)` генерирует `OPENJSON(@p) WITH (...)`, что валится на
SQL Server < 2016 (compat level < 130): ошибка 156 «Incorrect syntax near 'WITH'».
Решение уже применено в `Program.cs`:
```csharp
o.UseSqlServer(conn, sql => sql.UseCompatibilityLevel(120));
```
Не убирать. Новые запросы с фильтром по спискам теперь безопасны.

### 4.3. Авторизация: куку нельзя выставить из Blazor-контура
Вход/выход — только через HTTP-эндпоинты `/auth/login` и `/auth/logout` (minimal API
в `Program.cs`, `SignInAsync`/`SignOutAsync`, `.DisableAntiforgery()`). Формы входа/выхода
делаются обычным `<form method="post" data-enhance="false">`. Состояние авторизации в
компоненты отдаёт зарегистрированный `ServerAuthenticationStateProvider`
(`AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>()`).
Не пытаться логинить через интерактивный компонент.

### 4.4. Razor: имя класса страницы = имя файла
Инъекция сервиса в свойство с тем же именем, что и компонент, даёт CS0542.
Пример: в `Users.razor` нельзя `@inject UserService Users` — переименовано в `UserSvc`.

### 4.5. Razor: вложенные кавычки в `@onclick`
Не писать интерполяцию с двойными кавычками прямо в атрибуте
`@onclick="@(() => Foo("текст"))"` — ломает разбор. Выносить в метод и вызывать
`@onclick="() => Foo(arg)"`.

### 4.6. CSS sticky в таблице ввода
`position: sticky` для `th`/строк не работает при `border-collapse: collapse` и
`overflow: hidden` (они есть у базового `.grid`). Для таблицы ввода `.grid.sheet`
переопределено: `border-collapse: separate; border-spacing: 0; overflow: visible`,
а прокрутка — в контейнере `.sheet-scroll` (`overflow:auto; max-height: calc(100vh - 320px)`).
Липкие: `thead th { top:0 }`, `tr.section-row td { top:32px }` (значение top уже
подгонялось вручную — не трогать без причины).

### 4.7. Файлы на диске не чистятся каскадом
Каскад БД удаляет строки `WorkFiles`/`WorkEntries`, но **не файлы на диске**
(`App_Data/uploads`). При удалении записей/преподавателя/очистке — сначала удалять
файлы через `FileStorageService.Delete(...)`, потом строки. Смотри `RemoveEntries`,
`ConfirmDeleteTeacher`, `DoClear` в `Home.razor`.

### 4.8. Нельзя собрать без SQL Server
Приложение при старте создаёт/сидит БД. Для локального прогона нужен доступный SQL Server.

### 4.9. Два разных эндпоинта скачивания файлов — не путать
В `Program.cs` их два, с разной семантикой `id`:
- `/files/{id}` — **легаси**, `id` = `WorkEntry.Id`, отдаёт старое одиночное поле
  `WorkEntries.StoredFileName`. Новый код на него ссылаться не должен.
- `/workfiles/{id}` — **актуальный**, `id` = `WorkFile.Id` (множественные вложения).

Ссылки в `Home.razor` строятся по `WorkFile.Id`, поэтому там только `/workfiles/@f.Id`.
Ошибка уже была: ссылка вела на `/files/`, и по id вложения открывался либо 404, либо
чужой легаси-файл записи с совпавшим `WorkEntry.Id`.

### 4.10. Стиль работы
Общение по-русски. Отдавать готовые файлы целиком (не «замените строку»). Не добавлять
лишние зависимости. Excel-форма должна визуально совпадать с официальным бланком
(чёрно-белая, без заливок; жирные — только строки разделов, «Итого», заголовок).

---

## 5. Структура проекта

Всё под `src/PpsScoreApp/`.

```
Domain/            сущности
  Enums.cs         WorkSection (1..5, extension Title()/FullTitle()); ScoreInputKind (Fixed/Choice/Manual/Quantity)
  Department.cs     кафедра (Name, ShortName, HeadName)
  Teacher.cs        преподаватель (FullName, DepartmentId)
  WorkType.cs       вид работы (+ ScoreOption; FullNumber = "{section}.{Number}")
                    InputKind/FixedPoints/UnitPoints/Options используются только как
                    подсказка в колонке «Баллы» — ввод везде ручной числовой
  WorkEntry.cs      внесённая работа (TeacherId, WorkTypeId, AcademicYear, Semester, Points,
                    ScoreOptionId?, Quantity?, Comment, легаси StoredFileName/OriginalFileName,
                    навигация WorkFiles)
  WorkFile.cs       файл-вложение (WorkEntryId FK cascade, StoredFileName, OriginalFileName, CreatedAt)
  AppUser.cs        пользователь (Login uniq, DisplayName, PasswordHash, IsAdmin, IsActive,
                    TeacherId? — привязка к преподавателю; у админа null = доступ ко всем)
  CompletionStatus.cs  отметка «Заполнено» на (TeacherId, AcademicYear, Semester) — таблица Completions
  Remark.cs         замечание проверяющего на (TeacherId, AcademicYear, Semester, WorkTypeId?) —
                    таблица Remarks; WorkTypeId = null — общее замечание к таблице; статус
                    IsResolved/ResolvedAt/ResolvedBy

Data/
  AppDbContext.cs   DbSet-ы + связи + индексы (см. ниже)
  SeedData.Departments.cs   36 кафедр
  SeedData.WorkTypes.cs     57 видов работ по 5 разделам
  DbInitializer.cs  EnsureCreated + сид справочников + сид админа (admin/88863795)

Services/
  ScoringRules.cs       лимиты: SectionCap=50, TotalCap=100, TotalFloor=0; CapSection/CapTotal
  ReportService.cs      строит ReportResult (матрица баллов, разделы, итоги)
  ExcelExportService.cs официальная форма-матрица (альбом, повтор шапки, подпись, отд. лист на кафедру)
  WordExportService.cs  пояснения по каждому преподавателю (docx)
  PdfExportService.cs   PDF
  ExportBundleService.cs  BuildAsync → ZIP: Excel + папка на преподавателя (пояснения + Приложения/файлы)
  FileStorageService.cs   сохранение/чтение/удаление файлов (App_Data/uploads, Guid-имена)
  UserService.cs        валидация/создание/удаление пользователей, GetByLoginAsync (актуальные
                        права из БД), UpdateAccessAsync (роль + привязка к преподавателю)
  TeacherService.cs     справочник преподавателей: список, создание, изменение, удаление
                        (с чисткой файлов), LinkedAccountsAsync, EntryCountAsync
  PasswordHasher.cs     PBKDF2 (SHA256), формат "итерации.соль.хеш"
  AuditLogger.cs        лог по дням в папку Logs/ (singleton)
  CompletionService.cs  IsCompletedAsync / SetAsync / CompletedIdsAsync / StartedIdsAsync
  RemarkService.cs      ListAsync / AddAsync / SetResolvedAsync / DeleteAsync / OpenCountsAsync
  WorkInstructions.cs   статический словарь FullNumber → текст инструкции (из PDF), для «?»
                        57 записей — по числу видов работ
  Periods.cs            учебные годы, семестры

Models/ReportModels.cs  ReportRequest, TeacherReport, ReportResult и т.п.

Components/
  App.razor, Routes.razor (AuthorizeRouteView + RedirectToLogin), _Imports.razor
  Account/RedirectToLogin.razor
  Layout/MainLayout.razor (шапка, навигация под авторизацию, имя пользователя, «Выйти»)
  Pages/
    Home.razor    "/"        [Authorize]  — ВВОД показателей (главный экран)
    Report.razor  "/report"  [Authorize]  — отчёты, выбор преподавателей, выгрузка ZIP
    Users.razor   "/users"   [Authorize(Roles="Admin")] — управление пользователями
    Login.razor   "/login"   [AllowAnonymous] — форма входа (static SSR)
    Error.razor

wwwroot/app.css   все стили
wwwroot/app.js    window.downloadFile(filename, contentType, base64) — им Report.razor
                  отдаёт собранный ZIP; подключён в App.razor
Properties/PublishProfiles/FolderProfile.pubxml
                  публикация в папку bin/Release/net9.0/publish
                  (DeleteExistingFiles=false, ExcludeApp_Data=false — загруженные файлы
                  при публикации не затираются)
Program.cs        DI, авторизация, эндпоинты /auth/*, /files/{id}, /workfiles/{id},
                  QuestPDF-лицензия
README.md         прод-описание
```

---

## 6. Модель данных и связи (`AppDbContext`)

- WorkEntry → Teacher: **Cascade** (удаление преподавателя удаляет его работы).
- WorkEntry → WorkType: **Restrict**.
- WorkEntry → ScoreOption: **SetNull**.
- WorkFile → WorkEntry: **Cascade**.
- CompletionStatus → Teacher: **Cascade**.
- AppUser → Teacher: **SetNull** (удаление преподавателя оставляет учётку без привязки).
- ScoreOption → WorkType: **Cascade**.
- Remark → Teacher: **Cascade**; Remark → WorkType: **Restrict** (FK nullable).
- Индексы: `Department.Name` uniq, `AppUser.Login` uniq,
  `WorkEntry(TeacherId, AcademicYear, Semester, WorkTypeId)` uniq (защита от дублей
  при гонке автосохранения), `Completions(TeacherId, AcademicYear, Semester)` uniq,
  `Remarks(TeacherId, AcademicYear, Semester)`.

Замечание привязано к **строке** (виду работы), а не к `WorkEntry`: строка бывает пустой,
а очистка строки удаляет `WorkEntry` — замечание при этом должно остаться.

Периоды: учебный год — строка вида `"2025/26"`; семестр — int (1/2).

### Файлы: две новые таблицы (нужен SQL для существующей БД)
`WorkFiles` и `Completions` добавлены после первичного релиза. На уже созданной базе
их нет — их создают вручную:

```sql
CREATE TABLE [WorkFiles] (
    [Id] int NOT NULL IDENTITY,
    [WorkEntryId] int NOT NULL,
    [StoredFileName] nvarchar(260) NOT NULL,
    [OriginalFileName] nvarchar(260) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WorkFiles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkFiles_WorkEntries] FOREIGN KEY ([WorkEntryId])
        REFERENCES [WorkEntries]([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_WorkFiles_WorkEntryId] ON [WorkFiles]([WorkEntryId]);

CREATE TABLE [Completions] (
    [Id] int NOT NULL IDENTITY,
    [TeacherId] int NOT NULL,
    [AcademicYear] nvarchar(9) NOT NULL,
    [Semester] int NOT NULL,
    [IsCompleted] bit NOT NULL,
    [CompletedAt] datetime2 NULL,
    [CompletedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_Completions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Completions_Teachers] FOREIGN KEY ([TeacherId])
        REFERENCES [Teachers]([Id]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_Completions_Teacher_Period]
    ON [Completions]([TeacherId],[AcademicYear],[Semester]);
```

### Привязка учёток к преподавателям (SQL для существующей БД)
```sql
ALTER TABLE [Users] ADD [TeacherId] int NULL;
GO
ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Teachers] FOREIGN KEY ([TeacherId])
    REFERENCES [Teachers]([Id]) ON DELETE SET NULL;
CREATE UNIQUE INDEX [IX_Users_TeacherId] ON [Users]([TeacherId])
    WHERE [TeacherId] IS NOT NULL;
```
После этого всем не-админским учёткам нужно назначить преподавателя на `/users` —
без привязки они не смогут вносить показатели.

Легаси одиночные файлы (`WorkEntries.StoredFileName`) при необходимости переносятся:
```sql
INSERT INTO [WorkFiles]([WorkEntryId],[StoredFileName],[OriginalFileName],[CreatedAt])
SELECT [Id],[StoredFileName],[OriginalFileName], SYSUTCDATETIME()
FROM [WorkEntries] WHERE [StoredFileName] IS NOT NULL;
```

### Уникальность WorkEntry по виду работы (SQL для существующей БД)
Сначала дубли сливаются в запись с наименьшим Id (её и показывал экран ввода):
файлы дублей переносятся, сами дубли удаляются. Синтаксис совместим с SQL Server < 2016.
```sql
;WITH d AS (SELECT [Id], MIN([Id]) OVER (PARTITION BY [TeacherId],[AcademicYear],[Semester],[WorkTypeId]) AS [KeepId] FROM [WorkEntries])
UPDATE f SET [WorkEntryId] = d.[KeepId]
FROM [WorkFiles] f JOIN d ON f.[WorkEntryId] = d.[Id] WHERE d.[Id] <> d.[KeepId];

;WITH d AS (SELECT [Id], MIN([Id]) OVER (PARTITION BY [TeacherId],[AcademicYear],[Semester],[WorkTypeId]) AS [KeepId] FROM [WorkEntries])
INSERT INTO [WorkFiles]([WorkEntryId],[StoredFileName],[OriginalFileName],[CreatedAt])
SELECT d.[KeepId], e.[StoredFileName], e.[OriginalFileName], SYSUTCDATETIME()
FROM [WorkEntries] e JOIN d ON e.[Id] = d.[Id]
WHERE d.[Id] <> d.[KeepId] AND e.[StoredFileName] IS NOT NULL;

;WITH d AS (SELECT [Id], MIN([Id]) OVER (PARTITION BY [TeacherId],[AcademicYear],[Semester],[WorkTypeId]) AS [KeepId] FROM [WorkEntries])
DELETE e FROM [WorkEntries] e JOIN d ON e.[Id] = d.[Id] WHERE d.[Id] <> d.[KeepId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_WorkEntries_TeacherId_AcademicYear_Semester'
           AND [object_id] = OBJECT_ID('WorkEntries'))
    DROP INDEX [IX_WorkEntries_TeacherId_AcademicYear_Semester] ON [WorkEntries];
CREATE UNIQUE INDEX [IX_WorkEntries_TeacherId_AcademicYear_Semester_WorkTypeId]
    ON [WorkEntries]([TeacherId],[AcademicYear],[Semester],[WorkTypeId]);
```

### Роль проверяющего и замечания (SQL для существующей БД)
```sql
ALTER TABLE [Users] ADD [IsReviewer] bit NOT NULL CONSTRAINT [DF_Users_IsReviewer] DEFAULT 0;

CREATE TABLE [Remarks] (
    [Id] int NOT NULL IDENTITY,
    [TeacherId] int NOT NULL,
    [AcademicYear] nvarchar(9) NOT NULL,
    [Semester] int NOT NULL,
    [WorkTypeId] int NULL,
    [Text] nvarchar(2000) NOT NULL,
    [AuthorLogin] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsResolved] bit NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [ResolvedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_Remarks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Remarks_Teachers_TeacherId] FOREIGN KEY ([TeacherId])
        REFERENCES [Teachers]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Remarks_WorkTypes_WorkTypeId] FOREIGN KEY ([WorkTypeId])
        REFERENCES [WorkTypes]([Id])
);
CREATE INDEX [IX_Remarks_TeacherId_AcademicYear_Semester]
    ON [Remarks]([TeacherId],[AcademicYear],[Semester]);
CREATE INDEX [IX_Remarks_WorkTypeId] ON [Remarks]([WorkTypeId]);
```

---

## 7. Правила подсчёта (`Services/ScoringRules.cs`)

- Балл строки = введённое значение (сейчас баллы **целочисленные**; для всех видов —
  ручной числовой ввод; варианты/количество не используются для ввода, только как
  подсказка в колонке «Баллы»).
- Подытог раздела ограничен **50** (`CapSection`).
- Итог = сумма ограниченных разделов, затем **не более 100 и не менее 0** (`CapTotal`).

---

## 8. Авторизация

- Cookie-аутентификация без ASP.NET Core Identity.
- Админ по умолчанию сидится в `DbInitializer`: **login `admin`, пароль `88863795`**.
- Регистрация пользователей — только под админом на `/users`.
- `[Authorize]` на Home/Report, `[Authorize(Roles="Admin")]` на Users, `[AllowAnonymous]` на Login.
- Пароли — PBKDF2 (`PasswordHasher`).

### Разграничение доступа по преподавателю
- `AppUser.TeacherId` привязывает учётку к одному преподавателю. Обычный пользователь
  видит и правит **только его** показатели (Home) и отчитывается **только за него** (Report).
- Админ (`IsAdmin`) работает со всеми; у него `TeacherId` обычно null.
- Проверяющий (`IsReviewer`, совместим с `IsAdmin`; роль-клейм `Reviewer`) видит всех
  преподавателей на Home/Report **только на чтение**, пишет замечания, выгружает ZIP.
  Привязка к преподавателю у него необязательна; если есть — свою таблицу правит как обычно.
  На Home это флаги `SeesAll` (админ/проверяющий), `CanEdit` (админ или своя таблица),
  `CanResolve` (`CanEdit` или проверяющий). Серверные методы правки проверяют `CanEdit`.
- Замечания: добавить/удалить/«Вернуть в работу» — только проверяющий; «Исправлено» —
  тот, кто правит таблицу, или проверяющий.
- Права на страницах берутся **из БД** через `UserService.GetByLoginAsync(login)`, а не из
  клеймов куки — чтобы смена привязки админом действовала без перелогина. Клейм `TeacherId`
  в куке есть, но носит справочный характер.
- Учётка без привязки и без прав админа/проверяющего не может вводить показатели — страница показывает
  подсказку обратиться к администратору.
- Управление преподавателями (добавить/переименовать/перевести/удалить) живёт **только**
  на `/users`, вкладка «Преподаватели». С экрана ввода кнопки убраны намеренно.

---

## 9. Логирование (`AuditLogger`)

Папка **`Logs/`** в корне приложения, файл на каждый день `yyyy-MM-dd.txt`,
строки `время | логин | действие`. Логируются: вход/выход (в т.ч. неудачные),
создание/удаление пользователей, добавление/удаление преподавателей,
сохранение/очистка показателей, отметка «Заполнено». Папка в `.gitignore`.

---

## 10. Экспорт

- **Excel** (`ExcelExportService`): матрица «виды работ × преподаватели» в виде официального
  бланка. Альбом, A4, `FitToPages(1,0)`, повтор шапки (`SetRowsToRepeatAtTop`), двухстрочная
  шапка (над именами — «Фамилия И.О. штатного преподавателя», имена вертикально), подытоги
  разделов, «Итого», строка подписи зав. кафедрой. Отдельный лист на кафедру. Оформление
  строго ч/б, жирные — только разделы/«Итого»/заголовок.
- **Word** (`WordExportService`): на каждого преподавателя — пояснения по пунктам.
- **ZIP** (`ExportBundleService.BuildAsync`): общий Excel + на каждого преподавателя папка
  `{ФИО}/Пояснения_{ФИО}.docx` и `{ФИО}/Приложения/<файлы>` (из `WorkFiles` за период).
- **Имя файла доказательной базы** — по инструкции «Номер пункта_Порядковый номер.\*»
  (напр. `2.4_1.pdf`), формируется в `WorkFileNaming.Build`. Одно и то же имя используется
  в ZIP, при скачивании через `/workfiles/{id}` и в подписи файла на экране ввода;
  исходное имя остаётся в БД (`OriginalFileName`) и видно в подсказке. Нумерация —
  по порядку файлов внутри пункта.
- Отдача файлов пользователю — эндпоинт `/workfiles/{id}` (id = `WorkFile.Id`),
  `.RequireAuthorization()`. Легаси `/files/{id}` (id = `WorkEntry.Id`) — см. п. 4.9.
- ZIP уходит в браузер через JS-функцию `window.downloadFile` из `wwwroot/app.js`
  (base64 + `data:`-ссылка), см. `Report.ExportBundle`.

---

## 11. Ключевые страницы

### Home.razor (`/`) — ввод показателей
- Тулбар: преподаватель (админу — список, обычному пользователю — его собственный,
  без выбора), учебный год, семестр, кнопки «Заполнено» и «Перейти к отчётам».
  Кнопок «+Преподаватель» / «Удалить» здесь **нет** — управление на `/users`.
- Плашки подытогов по разделам + «Итого» с «?»-пояснением про лимиты; кнопка «Очистить таблицу».
- Таблица всех видов работ по разделам в прокручиваемом контейнере с липкими шапкой и
  заголовком раздела; у заголовка раздела — «Очистить раздел».
- Строка: №, вид работы (+«?» с инструкцией из `WorkInstructions`), «Баллы» (эталон-подсказка),
  «Заполнение» (числовой ввод, целые), «Балл» (расчёт), комментарий, «Файл» (несколько файлов,
  каждый со ссылкой и крестиком удаления).
- **Автосохранение** каждой строки при уходе с поля (`@bind:after="() => AutoSaveRow(row)"`).
  Отдельной кнопки «Сохранить» нет. Индикатор «Сохранено»/«Очищено» в тулбаре.
- Оверлей-спиннер (`busy`/`busyText`) для длительных операций (очистка, загрузка файлов).
- Одна строка ↔ одна запись `WorkEntry` за (преподаватель, период). Модель строки — вложенный
  класс `Row` (Value:int, Comment, EntryId, ExtraEntryIds, Files, NewFiles, RemoveFileIds).
- **Режим «Только просмотр»** (`!CanEdit`, у проверяющего на чужой таблице): поле ввода
  disabled, нет очистки, загрузки/удаления файлов, «Заполнено»; комментарий открывается
  на чтение; файлы скачиваются.
- **Замечания**: колонка «Замечания» (видна проверяющему или при наличии замечаний) —
  кнопка `⚑N` (открытые) / `✓` (все исправлены) / `+`; строка с открытыми замечаниями
  отмечена красной полосой слева. Кнопка «Общие замечания» в плашке итогов —
  замечания с `WorkTypeId = null`. Модалка: список, статусы, добавление (проверяющий).

### Users.razor (`/users`) — только админ
Две вкладки:
- **Учётные записи**: логин, имя, привязанный преподаватель, кафедра, роль, статус.
  «Доступ» — модалка смены роли и привязки (кафедра → преподаватель; показываются только
  свободные, т.е. без учётной записи). «Удалить» — с защитой от удаления себя и последнего админа.
- **Преподаватели**: добавление, переименование, перевод на другую кафедру, удаление
  (с предупреждением о числе работ и о том, что привязанная учётка останется без преподавателя).

### Report.razor (`/report`) — отчёты
- Админ: выбор года/семестра, список преподавателей по кафедрам (чекбоксы).
  Обычный пользователь: только свой преподаватель, выбран автоматически, панель выбора скрыта.
- **Цвет имени по статусу за период**: 🔴 красный — ничего не заполнено, 🟡 жёлтый (#ca8a04) —
  заполнение начато, 🟢 зелёный — отмечено «Заполнено». Сверху — легенда. Статусы берутся из
  `CompletionService.CompletedIdsAsync` (зелёный) и `StartedIdsAsync` (есть записи → жёлтый).
- Предпросмотр и выгрузка ZIP (`Bundle.BuildAsync`).
- У имени — `⚑N`, если есть открытые замечания проверяющего (`RemarkService.OpenCountsAsync`).

---

## 12. Соглашения по коду

- Nullable + ImplicitUsings включены.
- Доступ к БД — через `IDbContextFactory<AppDbContext>` (`await using var db = await ...CreateDbContextAsync()`),
  не общий scoped-контекст (безопасно для Blazor Server).
- Тексты интерфейса — по-русски.
- Не хранить в БД лишнее; файлы — на диске, в БД только метаданные.
- При изменениях, затрагивающих схему, — всегда прикладывать SQL для существующей базы.

---

## 13. История/решения (кратко)

- Изначально .NET 10 + EF Core 10 → понижено до .NET 9 + EF Core 9.0.9.
- Модель ввода эволюционировала: модалка по одной работе → цельная таблица-матрица с
  автосохранением; варианты/чекбоксы заменены на единый числовой ввод; баллы стали целыми.
- Добавлены: авторизация, аудит-лог, инструкции «?», множественные файлы (`WorkFiles`),
  статус «Заполнено» + цвета в отчётах (`Completions`), закрепление шапки, кнопки очистки,
  спиннеры.
- Excel многократно доводился до точного совпадения с бланком (ч/б, шрифты, подпись, печать).
- Добавлен профиль публикации в папку (`FolderProfile.pubxml`).
- Исправлена ссылка на вложения в `Home.razor`: была `/files/{WorkFile.Id}` — эндпоинт
  ожидает `WorkEntry.Id`, файлы не скачивались; стало `/workfiles/{WorkFile.Id}` (см. п. 4.9).
- Убрана двойная прокрутка на экране ввода: вместо `max-height: calc(100vh - 320px)`
  у `.sheet-scroll` — flex-цепочка `.app-shell → .app-main → .page-wrap → .sheet-page`.
- Комментарий вынесен в модальное окно (в ячейке — превью на 2 строки + карандаш),
  ширина рабочей области поднята до 1520px.
- Ссылки на `app.css`/`app.js` версионируются по `ModuleVersionId` (`?v=…`) — иначе
  браузер держал старый CSS после выкладки.
- Разграничение доступа: `AppUser.TeacherId`, управление преподавателями переехало
  с экрана ввода на `/users` (см. раздел 8).
- Уникальный индекс `WorkEntry(TeacherId, AcademicYear, Semester, WorkTypeId)` против
  дублей из-за гонки автосохранения (SQL со слиянием дублей — раздел 6).
- Роль проверяющего (`AppUser.IsReviewer`) и замечания к строкам (`Remarks`), см. разделы 6, 8, 11.
