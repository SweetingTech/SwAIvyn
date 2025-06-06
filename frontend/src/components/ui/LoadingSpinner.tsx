const LoadingSpinner = () => {
  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="h-16 w-16 border-8 border-gray-300 border-t-primary-500 rounded-full animate-spin" />
    </div>
  );
};

export default LoadingSpinner;
