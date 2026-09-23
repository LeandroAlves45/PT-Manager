const SESSION_CHANNEL = 'pt-manager.session.events';

/** O canal sincroniza invalidação; as credenciais nunca saem da memória do separador. */
interface SessionInvalidatedEvent {
  readonly type: 'session-invalidated';
}

let channel: BroadcastChannel | null = null;

function getChannel(): BroadcastChannel | null {
  if (typeof BroadcastChannel === 'undefined') return null;

  channel ??= new BroadcastChannel(SESSION_CHANNEL);
  return channel;
}

/** Informa os outros separadores de que devem esquecer a sessão local. */
export function publishSessionInvalidated(): void {
  const event: SessionInvalidatedEvent = { type: 'session-invalidated' };
  getChannel()?.postMessage(event);
}

/** Observa invalidações remotas sem transportar access token nem tokens CSRF. */
export function subscribeSessionInvalidated(listener: () => void): () => void {
  const currentChannel = getChannel();

  if (currentChannel === null) return () => undefined;

  const handleMessage = (event: MessageEvent<unknown>): void => {
    const value = event.data as Partial<SessionInvalidatedEvent> | null;

    if (value?.type === 'session-invalidated') {
      listener();
    }
  };

  currentChannel.addEventListener('message', handleMessage);
  return () => currentChannel.removeEventListener('message', handleMessage);
}
