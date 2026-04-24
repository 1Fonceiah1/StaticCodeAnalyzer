import * as vscode from 'vscode';
import { Analyzer } from './analyzer';
import { DiagnosticProvider } from './diagnostics';
import { Logger } from './logger';

// Главный файл расширения - точка входа
let analyzer: Analyzer;
let diagnosticProvider: DiagnosticProvider;

export function activate(context: vscode.ExtensionContext) {
    Logger.log('Static Code Analyzer extension activated');

    // Инициализация компонентов
    diagnosticProvider = new DiagnosticProvider();
    analyzer = new Analyzer(diagnosticProvider);

    // Регистрация команд
    registerCommands(context);

    // Регистрация обработчиков событий
    registerEventHandlers(context);

    // Показываем приветственное сообщение
    vscode.window.showInformationMessage(
        'Static Code Analyzer activated. Use Command Palette (Ctrl+Shift+P) to run analysis.',
        'Analyze Workspace',
        'Analyze Current File'
    ).then(selection => {
        if (selection === 'Analyze Workspace') {
            vscode.commands.executeCommand('staticCodeAnalyzer.analyzeWorkspace');
        } else if (selection === 'Analyze Current File') {
            vscode.commands.executeCommand('staticCodeAnalyzer.analyzeCurrentFile');
        }
    });
}

// Регистрация всех команд расширения
function registerCommands(context: vscode.ExtensionContext) {
    // Команда анализа всего workspace
    const analyzeWorkspaceCmd = vscode.commands.registerCommand(
        'staticCodeAnalyzer.analyzeWorkspace',
        () => analyzer.analyzeWorkspace()
    );

    // Команда анализа текущего файла
    const analyzeCurrentFileCmd = vscode.commands.registerCommand(
        'staticCodeAnalyzer.analyzeCurrentFile',
        () => analyzer.analyzeCurrentFile()
    );

    // Команда анализа выбранной папки
    const analyzeSelectedFolderCmd = vscode.commands.registerCommand(
        'staticCodeAnalyzer.analyzeSelectedFolder',
        (uri: vscode.Uri) => analyzer.analyzeFolder(uri)
    );

    // Команда очистки результатов
    const clearResultsCmd = vscode.commands.registerCommand(
        'staticCodeAnalyzer.clearResults',
        () => diagnosticProvider.clearDiagnostics()
    );

    // Добавляем все команды в контекст
    context.subscriptions.push(
        analyzeWorkspaceCmd,
        analyzeCurrentFileCmd,
        analyzeSelectedFolderCmd,
        clearResultsCmd
    );
}

// Регистрация обработчиков событий
function registerEventHandlers(context: vscode.ExtensionContext) {
    const config = vscode.workspace.getConfiguration('staticCodeAnalyzer');

    // Автоматический анализ при сохранении
    if (config.get<boolean>('runOnSave', false)) {
        const saveDisposable = vscode.workspace.onDidSaveTextDocument(document => {
            if (document.languageId === 'csharp') {
                analyzer.analyzeFile(document.uri.fsPath);
            }
        });
        context.subscriptions.push(saveDisposable);
    }

    // Очистка диагностик при закрытии файла
    const closeDisposable = vscode.workspace.onDidCloseTextDocument(document => {
        if (document.languageId === 'csharp') {
            diagnosticProvider.clearDiagnosticsForFile(document.uri);
        }
    });
    context.subscriptions.push(closeDisposable);

    // Обработка изменения конфигурации
    const configDisposable = vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('staticCodeAnalyzer')) {
            analyzer.loadConfiguration();
            Logger.log('Configuration reloaded');
        }
    });
    context.subscriptions.push(configDisposable);
}

export function deactivate() {
    Logger.log('Static Code Analyzer extension deactivated');
    diagnosticProvider?.dispose();
}
