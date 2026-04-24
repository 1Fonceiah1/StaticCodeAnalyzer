import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import { DiagnosticProvider } from './diagnostics';
import { Logger } from './logger';

// Модели для JSON ответа от анализатора
interface AnalysisResult {
    version: string;
    summary: {
        totalFiles: number;
        totalIssues: number;
        criticalCount: number;
        highCount: number;
        mediumCount: number;
        lowCount: number;
    };
    files: { path: string; issuesCount: number }[];
    issues: AnalysisIssue[];
    errors: { rule: string; file: string; message: string }[];
}

interface AnalysisIssue {
    filePath: string;
    line: number;
    column: number;
    severity: 'critical' | 'high' | 'medium' | 'low';
    type: string;
    code: string;
    rule: string;
    message: string;
    suggestion?: string;
    containingType?: string;
    method?: string;
}

// Класс для запуска CLI анализатора и обработки результатов
export class Analyzer {
    private diagnosticProvider: DiagnosticProvider;
    private executablePath: string = '';

    constructor(diagnosticProvider: DiagnosticProvider) {
        this.diagnosticProvider = diagnosticProvider;
        this.loadConfiguration();
    }

    // Загружает конфигурацию из VS Code settings
    public loadConfiguration(): void {
        const config = vscode.workspace.getConfiguration('staticCodeAnalyzer');
        
        // Путь к исполняемому файлу
        let exePath = config.get<string>('executablePath', '');
        if (!exePath) {
            // Пробуем найти в PATH или использовать стандартный путь
            exePath = this.findDefaultExecutablePath();
        }
        this.executablePath = exePath;
    }

    // Ищет исполняемый файл по умолчанию
    private findDefaultExecutablePath(): string {
        const possibleNames = [
            'StaticCodeAnalyzer.exe',
            'staticcodeanalyzer.exe',
            'StaticCodeAnalyzer',
            'static-code-analyzer'
        ];

        // Проверяем PATH
        for (const name of possibleNames) {
            try {
                const result = cp.execSync(`where ${name}`, { encoding: 'utf8' });
                if (result) {
                    return result.trim().split('\n')[0];
                }
            } catch {
                // Не найден, продолжаем
            }
        }

        // Проверяем стандартные пути установки
        const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
        const possiblePaths = [
            path.join(workspaceRoot, 'StaticCodeAnalyzer.exe'),
            path.join(workspaceRoot, 'bin', 'StaticCodeAnalyzer.exe'),
            path.join(workspaceRoot, '..', 'StaticCodeAnalyzer', 'bin', 'Release', 'net8.0-windows', 'StaticCodeAnalyzer.exe'),
            path.join(workspaceRoot, '..', 'StaticCodeAnalyzer', 'bin', 'Debug', 'net8.0-windows', 'StaticCodeAnalyzer.exe'),
        ];

        for (const p of possiblePaths) {
            if (this.fileExists(p)) {
                return p;
            }
        }

        Logger.log('Warning: Could not find StaticCodeAnalyzer executable');
        return '';
    }

    // Проверяет существование файла
    private fileExists(filePath: string): boolean {
        try {
            require('fs').accessSync(filePath);
            return true;
        } catch {
            return false;
        }
    }

    // Анализирует весь workspace
    public async analyzeWorkspace(): Promise<void> {
        const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceRoot) {
            vscode.window.showErrorMessage('No workspace folder open');
            return;
        }

        await this.runAnalysis(workspaceRoot, 'workspace');
    }

    // Анализирует текущий файл
    public async analyzeCurrentFile(): Promise<void> {
        const editor = vscode.window.activeTextEditor;
        if (!editor) {
            vscode.window.showErrorMessage('No active editor');
            return;
        }

        if (editor.document.languageId !== 'csharp') {
            vscode.window.showErrorMessage('Active file is not a C# file');
            return;
        }

        await this.runAnalysis(editor.document.uri.fsPath, 'file');
    }

    // Анализирует выбранную папку
    public async analyzeFolder(uri: vscode.Uri): Promise<void> {
        if (!uri) {
            vscode.window.showErrorMessage('No folder selected');
            return;
        }

        await this.runAnalysis(uri.fsPath, 'folder');
    }

    // Анализирует конкретный файл
    public async analyzeFile(filePath: string): Promise<void> {
        await this.runAnalysis(filePath, 'file');
    }

    // Запускает анализ через CLI
    private async runAnalysis(targetPath: string, scope: 'workspace' | 'folder' | 'file'): Promise<void> {
        if (!this.executablePath) {
            const result = await vscode.window.showErrorMessage(
                'Static Code Analyzer executable not found. Please configure the path in settings.',
                'Open Settings'
            );
            if (result === 'Open Settings') {
                vscode.commands.executeCommand('workbench.action.openSettings', 'staticCodeAnalyzer.executablePath');
            }
            return;
        }

        // Показываем прогресс
        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: `Running Static Code Analyzer on ${scope}...`,
            cancellable: false
        }, async (progress) => {
            try {
                const result = await this.executeAnalysis(targetPath);
                this.processResults(result);
                this.showSummary(result, scope);
            } catch (error) {
                const errorMessage = error instanceof Error ? error.message : String(error);
                Logger.log(`Analysis failed: ${errorMessage}`);
                vscode.window.showErrorMessage(`Analysis failed: ${errorMessage}`);
            }
        });
    }

    // Выполняет CLI команду анализа
    private executeAnalysis(targetPath: string): Promise<AnalysisResult> {
        return new Promise((resolve, reject) => {
            const config = vscode.workspace.getConfiguration('staticCodeAnalyzer');
            const severityFilter = config.get<string>('severityFilter', 'all');

            const args = [
                '--analyze', targetPath,
                '--output', 'json'
            ];

            if (severityFilter !== 'all') {
                args.push('--severity', severityFilter);
            }

            Logger.log(`Executing: ${this.executablePath} ${args.join(' ')}`);

            const process = cp.spawn(this.executablePath, args, {
                shell: true
            });

            let stdout = '';
            let stderr = '';

            process.stdout.on('data', (data) => {
                stdout += data.toString();
            });

            process.stderr.on('data', (data) => {
                stderr += data.toString();
            });

            process.on('close', (code) => {
                if (code === 2) {
                    reject(new Error(`Analyzer error: ${stderr || 'Unknown error'}`));
                    return;
                }

                try {
                    // Ищем JSON в выводе
                    const jsonMatch = stdout.match(/\{[\s\S]*\}/);
                    if (jsonMatch) {
                        const result = JSON.parse(jsonMatch[0]) as AnalysisResult;
                        resolve(result);
                    } else {
                        reject(new Error('No JSON output found from analyzer'));
                    }
                } catch (parseError) {
                    reject(new Error(`Failed to parse analyzer output: ${parseError}`));
                }
            });

            process.on('error', (error) => {
                reject(new Error(`Failed to execute analyzer: ${error.message}`));
            });

            // Таймаут 60 секунд
            setTimeout(() => {
                process.kill();
                reject(new Error('Analysis timed out after 60 seconds'));
            }, 60000);
        });
    }

    // Обрабатывает результаты анализа
    private processResults(result: AnalysisResult): void {
        Logger.log(`Analysis complete: ${result.summary.totalIssues} issues found`);
        
        // Группируем issues по файлам
        const issuesByFile = new Map<string, AnalysisIssue[]>();
        for (const issue of result.issues) {
            const issues = issuesByFile.get(issue.filePath) || [];
            issues.push(issue);
            issuesByFile.set(issue.filePath, issues);
        }

        // Обновляем диагностики для каждого файла
        for (const [filePath, issues] of issuesByFile) {
            const uri = vscode.Uri.file(filePath);
            this.diagnosticProvider.updateDiagnostics(uri, issues);
        }

        // Логируем ошибки анализа
        if (result.errors.length > 0) {
            Logger.log(`Analysis errors: ${result.errors.length}`);
            for (const error of result.errors) {
                Logger.log(`  ${error.rule}: ${error.message}`);
            }
        }
    }

    // Показывает сводку результатов
    private showSummary(result: AnalysisResult, scope: string): void {
        const { criticalCount, highCount, mediumCount, lowCount, totalIssues } = result.summary;
        
        const messageParts: string[] = [];
        if (criticalCount > 0) messageParts.push(`${criticalCount} critical`);
        if (highCount > 0) messageParts.push(`${highCount} high`);
        if (mediumCount > 0) messageParts.push(`${mediumCount} medium`);
        if (lowCount > 0) messageParts.push(`${lowCount} low`);

        const message = messageParts.length > 0 
            ? `Found ${messageParts.join(', ')} issues in ${scope}`
            : `No issues found in ${scope}`;

        if (criticalCount > 0 || highCount > 0) {
            vscode.window.showWarningMessage(message, 'Show Problems').then(selection => {
                if (selection === 'Show Problems') {
                    vscode.commands.executeCommand('workbench.panel.markers.view.focus');
                }
            });
        } else if (totalIssues > 0) {
            vscode.window.showInformationMessage(message);
        } else {
            vscode.window.showInformationMessage(message);
        }
    }
}
