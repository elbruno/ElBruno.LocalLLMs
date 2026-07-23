import * as signalR from '@microsoft/signalr';
import type { AgentMessage } from '../types/agentTypes';

export type AgentMessageHandler = (msg: AgentMessage) => void;
export type TaskCompleteHandler = (taskId: string, result: string) => void;
export type TaskErrorHandler = (taskId: string, error: string) => void;
export type StatusChangeHandler = (status: string) => void;

export class AgentHubClient {
  private connection: signalR.HubConnection;

  constructor(
    private readonly onMessage: AgentMessageHandler,
    private readonly onComplete: TaskCompleteHandler,
    private readonly onError: TaskErrorHandler,
    private readonly onStatusChange: StatusChangeHandler,
  ) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/agent')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('AgentMessage', (msg: AgentMessage) => this.onMessage(msg));
    this.connection.on('TaskComplete', (taskId: string, result: string) => this.onComplete(taskId, result));
    this.connection.on('TaskError', (taskId: string, error: string) => this.onError(taskId, error));

    this.connection.onreconnecting(() => this.onStatusChange('reconnecting'));
    this.connection.onreconnected(() => this.onStatusChange('connected'));
    this.connection.onclose(() => this.onStatusChange('disconnected'));
  }

  async start(): Promise<void> {
    this.onStatusChange('connecting');
    await this.connection.start();
    this.onStatusChange('connected');
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }

  async submitTask(taskId: string, prompt: string, workingDirectory?: string): Promise<void> {
    await this.connection.invoke('SubmitTask', { taskId, prompt, workingDirectory });
  }

  async cancelTask(taskId: string): Promise<void> {
    await this.connection.invoke('CancelTask', taskId);
  }
}
