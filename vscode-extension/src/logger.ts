import * as vscode from 'vscode';

// Простой логгер для расширения
export class Logger {
    private static outputChannel: vscode.OutputChannel | undefined;

    // Инициализирует output channel
    private static getOutputChannel(): vscode.OutputChannel {
        if (!this.outputChannel) {
            this.outputChannel = vscode.window.createOutputChannel('Static Code Analyzer');
        }
        return this.outputChannel;
    }

    // Логирует сообщение
    public static log(message: string): void {
        const timestamp = new Date().toISOString();
        const channel = this.getOutputChannel();
        channel.appendLine(`[${timestamp}] ${message}`);
    }

    // Показывает output channel
    public static show(): void {
        this.getOutputChannel().show();
    }

    // Очищает output channel
    public static clear(): void {
        this.getOutputChannel().clear();
    }

    // Удаляет output channel
    public static dispose(): void {
        if (this.outputChannel) {
            this.outputChannel.dispose();
            this.outputChannel = undefined;
        }
    }
}
