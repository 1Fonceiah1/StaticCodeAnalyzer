# Static Code Analyzer VS Code Extension

Расширение для интеграции C# Static Code Analyzer с Visual Studio Code.

## Возможности

- **Анализ Workspace** - анализ всего проекта
- **Анализ текущего файла** - быстрый анализ активного файла
- **Анализ выбранной папки** - контекстное меню в Explorer
- **Проблемы в реальном времени** - отображение в Problems панели
- **Inline decorations** - подсветка проблем прямо в коде
- **Автоматический анализ при сохранении** - опционально

## Команды

| Команда | Описание | Горячая клавиша |
|---------|----------|-----------------|
| `Static Code Analyzer: Analyze Workspace` | Анализ всего проекта | - |
| `Static Code Analyzer: Analyze Current File` | Анализ текущего файла | - |
| `Static Code Analyzer: Analyze Folder` | Анализ выбранной папки | - |
| `Static Code Analyzer: Clear Results` | Очистка результатов | - |

## Настройки

```json
{
  "staticCodeAnalyzer.executablePath": "C:/Path/To/StaticCodeAnalyzer.exe",
  "staticCodeAnalyzer.severityFilter": "all",
  "staticCodeAnalyzer.runOnSave": false,
  "staticCodeAnalyzer.autoFixOnSave": false
}
```

### `executablePath`

Путь к исполняемому файлу анализатора. Если не указан, расширение попытается найти его автоматически в:
- PATH
- Корне workspace
- Стандартных путях сборки

### `severityFilter`

Фильтр по серьезности проблем:
- `all` - все проблемы
- `critical` - только критические
- `high` - высокий и выше
- `medium` - средний и выше
- `low` - только низкий

### `runOnSave`

Автоматически запускать анализ при сохранении файла.

## Установка

1. Соберите расширение:
```bash
cd vscode-extension
npm install
npm run compile
```

2. Установите в VS Code:
- Откройте VS Code
- Перейдите в Extensions view
- Click "..." → "Install from VSIX"
- Выберите `static-code-analyzer-1.0.0.vsix`

Или запустите в режиме разработки:
```bash
# В VS Code нажмите F5
```

## Сборка VSIX

```bash
npm install -g @vscode/vsce
vsce package
```

## Требования

- VS Code 1.85.0 или выше
- .NET 8.0 Runtime
- Собранный StaticCodeAnalyzer.exe
