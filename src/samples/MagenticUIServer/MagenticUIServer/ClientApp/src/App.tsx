import { useState, useEffect, useRef, useCallback } from 'react';
import { AgentHubClient } from './hub/agentHubClient';
import type { AgentMessage, HubStatus } from './types/agentTypes';

const styles: Record<string, React.CSSProperties> = {
  app: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    background: '#0f0f13',
    color: '#e0e0e0',
    fontFamily: 'system-ui, -apple-system, sans-serif',
  },
  header: {
    padding: '12px 20px',
    background: '#1a1a24',
    borderBottom: '1px solid #2a2a3a',
    display: 'flex',
    alignItems: 'center',
    gap: 12,
  },
  title: { fontSize: 18, fontWeight: 700, color: '#a78bfa' },
  statusDot: (status: HubStatus): React.CSSProperties => ({
    width: 10,
    height: 10,
    borderRadius: '50%',
    background: status === 'connected' ? '#4ade80' : status === 'connecting' ? '#facc15' : '#f87171',
    flexShrink: 0,
  }),
  statusText: { fontSize: 12, color: '#9ca3af' },
  messageList: {
    flex: 1,
    overflowY: 'auto',
    padding: '16px 20px',
    display: 'flex',
    flexDirection: 'column',
    gap: 10,
  },
  messageCard: (role: string): React.CSSProperties => ({
    background: role === 'system' ? '#1e1e2e' : role === 'assistant' ? '#1a2535' : '#1f2a1f',
    border: `1px solid ${role === 'system' ? '#3a3a5c' : role === 'assistant' ? '#2a4a6a' : '#2a4a2a'}`,
    borderRadius: 8,
    padding: '10px 14px',
    maxWidth: '80%',
    alignSelf: role === 'user' ? 'flex-end' : 'flex-start',
  }),
  agentLabel: { fontSize: 11, color: '#6b7280', marginBottom: 4 },
  messageText: { fontSize: 14, lineHeight: 1.6, whiteSpace: 'pre-wrap', wordBreak: 'break-word' },
  completeBanner: {
    background: '#14532d',
    border: '1px solid #166534',
    borderRadius: 8,
    padding: '10px 14px',
    fontSize: 13,
    color: '#86efac',
    maxWidth: '80%',
    alignSelf: 'flex-start',
  },
  errorBanner: {
    background: '#450a0a',
    border: '1px solid #7f1d1d',
    borderRadius: 8,
    padding: '10px 14px',
    fontSize: 13,
    color: '#fca5a5',
    maxWidth: '80%',
    alignSelf: 'flex-start',
  },
  inputRow: {
    padding: '12px 20px',
    background: '#1a1a24',
    borderTop: '1px solid #2a2a3a',
    display: 'flex',
    gap: 10,
  },
  textarea: {
    flex: 1,
    background: '#12121a',
    border: '1px solid #3a3a5c',
    borderRadius: 8,
    color: '#e0e0e0',
    padding: '10px 14px',
    fontSize: 14,
    resize: 'vertical',
    minHeight: 60,
    outline: 'none',
  },
  button: (disabled: boolean): React.CSSProperties => ({
    background: disabled ? '#3a3a5c' : '#7c3aed',
    color: disabled ? '#6b7280' : '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '0 20px',
    fontWeight: 600,
    fontSize: 14,
    cursor: disabled ? 'not-allowed' : 'pointer',
    transition: 'background 0.2s',
  }),
  cancelButton: {
    background: '#7f1d1d',
    color: '#fca5a5',
    border: 'none',
    borderRadius: 8,
    padding: '0 16px',
    fontWeight: 600,
    fontSize: 13,
    cursor: 'pointer',
  },
};

interface DisplayItem {
  type: 'message' | 'complete' | 'error';
  content: AgentMessage | { taskId: string; text: string };
}

export default function App() {
  const [status, setStatus] = useState<HubStatus>('disconnected');
  const [prompt, setPrompt] = useState('');
  const [items, setItems] = useState<DisplayItem[]>([]);
  const [currentTaskId, setCurrentTaskId] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const hubRef = useRef<AgentHubClient | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = useCallback(() => {
    setTimeout(() => {
      listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
    }, 50);
  }, []);

  useEffect(() => {
    const client = new AgentHubClient(
      (msg) => {
        setItems(prev => [...prev, { type: 'message', content: msg }]);
        scrollToBottom();
      },
      (taskId, result) => {
        setItems(prev => [...prev, { type: 'complete', content: { taskId, text: result } }]);
        setRunning(false);
        setCurrentTaskId(null);
        scrollToBottom();
      },
      (taskId, error) => {
        setItems(prev => [...prev, { type: 'error', content: { taskId, text: error } }]);
        setRunning(false);
        setCurrentTaskId(null);
        scrollToBottom();
      },
      (s) => setStatus(s as HubStatus),
    );

    hubRef.current = client;
    client.start().catch(() => setStatus('error'));

    return () => { client.stop(); };
  }, [scrollToBottom]);

  const handleSubmit = useCallback(async () => {
    if (!prompt.trim() || !hubRef.current || status !== 'connected' || running) return;
    const taskId = `task-${Date.now()}`;
    setCurrentTaskId(taskId);
    setRunning(true);
    setItems(prev => [...prev, {
      type: 'message',
      content: { agentName: 'You', role: 'user', text: prompt.trim(), round: 0 },
    }]);
    setPrompt('');
    scrollToBottom();
    await hubRef.current.submitTask(taskId, prompt.trim());
  }, [prompt, status, running, scrollToBottom]);

  const handleCancel = useCallback(async () => {
    if (!currentTaskId || !hubRef.current) return;
    await hubRef.current.cancelTask(currentTaskId);
  }, [currentTaskId]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleSubmit();
    }
  }, [handleSubmit]);

  return (
    <div style={styles.app}>
      <div style={styles.header}>
        <div style={styles.statusDot(status)} />
        <span style={styles.title}>🧲 Magentic UI</span>
        <span style={styles.statusText}>{status}</span>
      </div>

      <div ref={listRef} style={styles.messageList}>
        {items.length === 0 && (
          <div style={{ color: '#4b5563', fontSize: 14, textAlign: 'center', marginTop: 40 }}>
            Enter a task below to start the Magentic agent session.
          </div>
        )}
        {items.map((item, i) => {
          if (item.type === 'message') {
            const msg = item.content as AgentMessage;
            return (
              <div key={i} style={styles.messageCard(msg.role)}>
                <div style={styles.agentLabel}>
                  {msg.agentName} {msg.round > 0 ? `· round ${msg.round}` : ''}
                </div>
                <div style={styles.messageText}>{msg.text}</div>
              </div>
            );
          }
          if (item.type === 'complete') {
            const c = item.content as { taskId: string; text: string };
            return (
              <div key={i} style={styles.completeBanner}>
                ✅ Task complete · {c.taskId}<br />
                <span style={{ color: '#d1fae5' }}>{c.text}</span>
              </div>
            );
          }
          const e = item.content as { taskId: string; text: string };
          return (
            <div key={i} style={styles.errorBanner}>
              ❌ Error · {e.taskId}<br />
              <span>{e.text}</span>
            </div>
          );
        })}
      </div>

      <div style={styles.inputRow}>
        <textarea
          style={styles.textarea}
          value={prompt}
          onChange={e => setPrompt(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Describe your task… (Ctrl+Enter to submit)"
          disabled={running}
        />
        {running ? (
          <button style={styles.cancelButton} onClick={handleCancel}>Cancel</button>
        ) : (
          <button
            style={styles.button(status !== 'connected' || !prompt.trim())}
            disabled={status !== 'connected' || !prompt.trim()}
            onClick={handleSubmit}
          >
            Run
          </button>
        )}
      </div>
    </div>
  );
}
