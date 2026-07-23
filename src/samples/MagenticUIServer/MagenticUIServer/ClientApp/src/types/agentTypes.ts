export interface AgentMessage {
  agentName: string;
  role: 'system' | 'assistant' | 'user';
  text: string;
  round: number;
}

export interface TaskRequest {
  taskId: string;
  prompt: string;
  workingDirectory?: string;
}

export type HubStatus = 'disconnected' | 'connecting' | 'connected' | 'error';
