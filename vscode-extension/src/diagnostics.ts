import * as vscode from 'vscode';
import { Logger } from './logger';

// Интерфейс для issue из JSON ответа
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

// Провайдер диагностик для VS Code
export class DiagnosticProvider {
    private diagnosticCollection: vscode.DiagnosticCollection;

    constructor() {
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('staticCodeAnalyzer');
    }

    // Обновляет диагностики для файла
    public updateDiagnostics(uri: vscode.Uri, issues: AnalysisIssue[]): void {
        const diagnostics: vscode.Diagnostic[] = issues.map(issue => this.createDiagnostic(issue));
        this.diagnosticCollection.set(uri, diagnostics);
        Logger.log(`Updated ${diagnostics.length} diagnostics for ${uri.fsPath}`);
    }

    // Создает диагностику из issue
    private createDiagnostic(issue: AnalysisIssue): vscode.Diagnostic {
        const range = new vscode.Range(
            Math.max(0, issue.line - 1),
            Math.max(0, issue.column - 1),
            Math.max(0, issue.line - 1),
            1000 // Конец строки
        );

        const severity = this.mapSeverity(issue.severity);
        const diagnostic = new vscode.Diagnostic(
            range,
            `[${issue.rule}] ${issue.message}${issue.suggestion ? `\nSuggestion: ${issue.suggestion}` : ''}`,
            severity
        );

        // Дополнительные данные
        diagnostic.code = issue.code;
        diagnostic.source = 'Static Code Analyzer';

        // Добавляем метаданные для quick fixes
        (diagnostic as any).data = {
            suggestion: issue.suggestion,
            containingType: issue.containingType,
            method: issue.method
        };

        return diagnostic;
    }

    // Преобразует severity из анализатора в VS Code DiagnosticSeverity
    private mapSeverity(severity: string): vscode.DiagnosticSeverity {
        switch (severity.toLowerCase()) {
            case 'critical':
                return vscode.DiagnosticSeverity.Error;
            case 'high':
                return vscode.DiagnosticSeverity.Error;
            case 'medium':
                return vscode.DiagnosticSeverity.Warning;
            case 'low':
                return vscode.DiagnosticSeverity.Information;
            default:
                return vscode.DiagnosticSeverity.Warning;
        }
    }

    // Очищает все диагностики
    public clearDiagnostics(): void {
        this.diagnosticCollection.clear();
        Logger.log('All diagnostics cleared');
    }

    // Очищает диагностики для конкретного файла
    public clearDiagnosticsForFile(uri: vscode.Uri): void {
        this.diagnosticCollection.delete(uri);
        Logger.log(`Diagnostics cleared for ${uri.fsPath}`);
    }

    // Освобождает ресурсы
    public dispose(): void {
        this.diagnosticCollection.dispose();
    }
}

// Провайдер code actions (quick fixes)
export class QuickFixProvider implements vscode.CodeActionProvider {
    public static readonly providedCodeActionKinds = [
        vscode.CodeActionKind.QuickFix
    ];

    provideCodeActions(
        document: vscode.TextDocument,
        range: vscode.Range | vscode.Selection,
        context: vscode.CodeActionContext,
        token: vscode.CancellationToken
    ): vscode.CodeAction[] {
        const actions: vscode.CodeAction[] = [];

        for (const diagnostic of context.diagnostics) {
            if (diagnostic.source === 'Static Code Analyzer') {
                // Пытаемся извлечь suggestion из сообщения
                const suggestionMatch = diagnostic.message.match(/Suggestion:\s*(.+)$/m);
                if (suggestionMatch) {
                    const suggestion = suggestionMatch[1].trim();
                    const action = new vscode.CodeAction(
                        `Apply fix: ${suggestion.substring(0, 50)}${suggestion.length > 50 ? '...' : ''}`,
                        vscode.CodeActionKind.QuickFix
                    );
                    action.diagnostics = [diagnostic];
                    action.isPreferred = true;
                    
                    // Пока что просто показываем информацию о предложенном исправлении
                    action.command = {
                        command: 'staticCodeAnalyzer.showSuggestion',
                        title: 'Show Suggestion',
                        arguments: [suggestion]
                    };
                    
                    actions.push(action);
                }
            }
        }

        return actions;
    }
}
