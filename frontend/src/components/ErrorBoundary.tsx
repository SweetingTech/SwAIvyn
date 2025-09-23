import { Component, ErrorInfo, ReactNode } from 'react';

type ErrorBoundaryProps = {
  children: ReactNode;
  /**
   * Optional fallback element or render function that will be displayed when an error is caught.
   */
  fallback?: ReactNode | ((args: { error: Error | null; resetErrorBoundary: () => void }) => ReactNode);
  /**
   * Called when the boundary is reset either manually or when children recover.
   */
  onReset?: () => void;
};

type ErrorBoundaryState = {
  hasError: boolean;
  error: Error | null;
};

/**
 * React error boundary that prevents uncaught component errors from crashing the application UI.
 *
 * The component logs errors to the console and renders a fallback UI while allowing callers to
 * provide custom fallback content and reset handlers.
 */
class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = {
    hasError: false,
    error: null,
  };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('ErrorBoundary caught an error', error, errorInfo);
  }

  private resetErrorBoundary = () => {
    this.setState({ hasError: false, error: null });
    this.props.onReset?.();
  };

  render() {
    if (this.state.hasError) {
      const { fallback } = this.props;

      if (typeof fallback === 'function') {
        return fallback({ error: this.state.error, resetErrorBoundary: this.resetErrorBoundary });
      }

      if (fallback) {
        return fallback;
      }

      return (
        <div className="flex h-screen flex-col items-center justify-center gap-4 bg-gray-50 p-6 text-center text-gray-800">
          <h1 className="text-2xl font-semibold">Something went wrong</h1>
          {this.state.error?.message && (
            <p className="max-w-md text-sm text-gray-600">{this.state.error.message}</p>
          )}
          <button
            type="button"
            onClick={this.resetErrorBoundary}
            className="rounded bg-primary-500 px-4 py-2 text-white shadow hover:bg-primary-600"
          >
            Try again
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;

