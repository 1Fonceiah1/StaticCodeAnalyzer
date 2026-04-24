# Static Code Analyzer - VS Code Extension Integration

Полная интеграция C# Static Code Analyzer с Visual Studio Code через расширение.

## Структура проекта

```
StaticCodeAnalyzer/
├── Cli/                          # CLI режим для анализатора
│   ├── CliOptions.cs            # Парсинг аргументов командной строки
│   ├── CliAnalyzer.cs           # Логика CLI анализа
│   └── JsonOutput.cs            # JSON модели вывода
├── vscode-extension/            # VS Code расширение (TypeScript)
│   ├── src/
│   │   ├── extension.ts         # Точка входа
│   │   ├── analyzer.ts          # Запуск CLI + парсинг JSON
│   │   ├── diagnostics.ts       # Отображение проблем
│   │   └── logger.ts            # Логирование
│   ├── package.json             # Манифест расширения
│   └── tsconfig.json            # Конфиг TypeScript
├── Analysis/                     # Исходный анализатор (C#)
└── Program.cs                   # Точка входа (WPF + CLI)
```

## Быстрый старт

### 1. Сборка C# анализатора

```bash
# Собрать Release версию
dotnet build -c Release

# Или запустить для тестирования CLI
dotnet run -- --analyze "C:\path\to\project" --output json
```

### 2. Установка VS Code расширения

```bash
cd vscode-extension

# Установить зависимости (требуется Node.js и npm)
npm install

# Скомпилировать TypeScript
npm run compile

# Открыть в VS Code для отладки
code .
# Затем нажмите F5
```

## Использование CLI режима

### Базовые команды

```bash
# Анализ файла с JSON выводом
StaticCodeAnalyzer.exe --analyze "C:\Project\MyClass.cs" --output json

# Анализ директории
StaticCodeAnalyzer.exe --analyze "C:\Project" --output json --recursive

# Фильтр по серьезности
StaticCodeAnalyzer.exe --analyze "C:\Project" --severity high

# Консольный вывод
StaticCodeAnalyzer.exe --analyze "C:\Project" --output console
```

### Примеры JSON вывода

```json
{
  "version": "1.0.0",
  "summary": {
    "totalFiles": 10,
    "totalIssues": 25,
    "criticalCount": 2,
    "highCount": 8,
    "mediumCount": 10,
    "lowCount": 5
  },
  "files": [...],
  "issues": [
    {
      "filePath": "C:\\Project\\MyClass.cs",
      "line": 42,
      "column": 15,
      "severity": "high",
      "type": "Code Smell",
      "code": "SA001",
      "rule": "MethodComplexityRule",
      "message": "Method has too high complexity (25)",
      "suggestion": "Consider refactoring into smaller methods"
    }
  ],
  "errors": []
}
```

## Функции VS Code расширения

### Команды

| Команда | Описание |
|---------|----------|
| `Static Code Analyzer: Analyze Workspace` | Анализ всего открытого workspace |
| `Static Code Analyzer: Analyze Current File` | Анализ активного .cs файла |
| `Static Code Analyzer: Analyze Folder` | Анализ выбранной папки (из контекстного меню) |
| `Static Code Analyzer: Clear Results` | Очистка всех диагностик |

### Настройки

Откройте VS Code Settings (Ctrl+,) и найдите "Static Code Analyzer":

```json
{
  "staticCodeAnalyzer.executablePath": "C:\\Path\\To\\StaticCodeAnalyzer.exe",
  "staticCodeAnalyzer.severityFilter": "all",
  "staticCodeAnalyzer.runOnSave": true
}
```

| Настройка | Описание | Значения |
|-----------|----------|----------|
| `executablePath` | Путь к .exe файлу анализатора | Путь или пусто для авто-поиска |
| `severityFilter` | Фильтр проблем | all/critical/high/medium/low |
| `runOnSave` | Авто-анализ при сохранении | true/false |

### UI интеграция

- **Problems Panel** - все найденные проблемы сгруппированы по файлам
- **Inline Decorations** - подсветка строк с проблемами
- **Hover Information** - описание проблемы при наведении
- **Context Menu** - анализ папки/файла через правый клик в Explorer

## Архитектура интеграции

```
┌─────────────────────────────────────────────────────────────┐
│                     VS Code Extension                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Commands   │  │  Diagnostics │  │    Logger    │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────┬────────────────────────────────────┘
                          │ spawns process
                          ▼
┌─────────────────────────────────────────────────────────────┐
│              StaticCodeAnalyzer.exe (CLI)                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  CliParser   │  │AnalyzerEngine│  │  JSON Output │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                    Analysis Rules (C#)                       │
│  - NamingConventionRule    - AsyncAwaitRule                 │
│  - MethodComplexityRule    - ThreadSafetyRule               │
│  - UnusedVariableRule      - SecurityVulnerabilitiesRule    │
│  - MagicNumbersRule        - CodeDuplicationRule            │
│  - EmptyCatchBlockRule     - And more...                    │
└─────────────────────────────────────────────────────────────┘
```

## Разработка

### Добавление нового правила

1. Создайте класс правила в `Analysis/Rules/`
2. Реализуйте интерфейс `IAnalyzerRule`
3. Добавьте в `AnalyzerEngine._rules`
4. Протестируйте через CLI: `dotnet run -- --analyze <path>`

### Отладка расширения

1. Откройте `vscode-extension/` в VS Code
2. Нажмите F5 (запустится Extension Development Host)
3. В новом окне откройте тестовый C# проект
4. Запустите команды расширения через Command Palette (Ctrl+Shift+P)

## Требования

- **C# Analyzer**: .NET 8.0 SDK
- **VS Code Extension**: Node.js 18+, VS Code 1.85+
- **ОС**: Windows (для WPF + CLI)

## Лицензия

MIT
