import { useEffect, useState, useCallback } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Message } from '../types/chat';
import configService from '../services/configService';

export function useChatHub() {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);

  useEffect(() => {
    const setupConnection = async () => {
      try {
        const hubUrl = await configService.getChatHubUrl();
        const newConnection = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Information)
          .build();

        setConnection(newConnection);
      } catch (error) {
        console.error('Failed to get chat hub URL:', error);
      }
    };

    setupConnection();
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          console.log('Connected to chat hub');

          connection.on('ReceiveMessage', (user: string, message: string) => {
            const newMessage: Message = {
              id: Date.now().toString(),
              sender: user === 'ai' ? 'ai' : 'user',
              text: message,
              timestamp: new Date().toISOString(),
            };
            setMessages(prev => [...prev, newMessage]);
          });

          connection.on('UserJoined', (connectionId: string) => {
            console.log(`User joined: ${connectionId}`);
          });

          connection.on('UserLeft', (connectionId: string) => {
            console.log(`User left: ${connectionId}`);
          });
        })
        .catch(e => console.error('Connection failed: ', e));
    }
  }, [connection]);

  const sendMessage = useCallback(async (user: string, message: string) => {
    if (connection && connection.state === 'Connected') {
      try {
        await connection.invoke('SendMessage', user, message);
      } catch (e) {
        console.error('Send message failed: ', e);
      }
    }
  }, [connection]);

  return { messages, sendMessage };
}
