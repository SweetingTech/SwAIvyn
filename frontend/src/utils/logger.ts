import { runtimeConfig } from '../config';

type LogLevel = 'debug' | 'info' | 'warn' | 'error';

type LogContext = Record<string, unknown> | undefined;

type Logger = {
  debug: (message: string, context?: LogContext) => void;
  info: (message: string, context?: LogContext) => void;
  warn: (message: string, context?: LogContext) => void;
  error: (message: string, context?: LogContext) => void;
};

const consoleMethod: Record<LogLevel, keyof Console> = {
  debug: 'debug',
  info: 'info',
  warn: 'warn',
  error: 'error',
};

function emit(level: LogLevel, message: string, context?: LogContext) {
  const payload = context ? { ...context } : undefined;
  if (payload) {
    // Remove undefined values for cleaner output
    Object.keys(payload).forEach((key) => {
      if (payload[key] === undefined) {
        delete payload[key];
      }
    });
  }

  if (level === 'debug' && !runtimeConfig.isDevelopment) {
    return;
  }

  const method = consoleMethod[level];
  if (payload && Object.keys(payload).length > 0) {
    console[method](`[${level.toUpperCase()}] ${message}`, payload);
  } else {
    console[method](`[${level.toUpperCase()}] ${message}`);
  }
}

export const logger: Logger = {
  debug: (message, context) => emit('debug', message, context),
  info: (message, context) => emit('info', message, context),
  warn: (message, context) => emit('warn', message, context),
  error: (message, context) => emit('error', message, context),
};

export function createPrefixedLogger(scope: string): Logger {
  const prefix = scope ? `${scope}: ` : '';
  return {
    debug: (message, context) => logger.debug(`${prefix}${message}`, context),
    info: (message, context) => logger.info(`${prefix}${message}`, context),
    warn: (message, context) => logger.warn(`${prefix}${message}`, context),
    error: (message, context) => logger.error(`${prefix}${message}`, context),
  };
}
