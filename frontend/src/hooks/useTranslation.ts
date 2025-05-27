import { useTranslation as useI18nTranslation } from 'react-i18next';

export const useTranslation = () => {
  const { t, i18n } = useI18nTranslation();

  const changeLanguage = async (language: string) => {
    await i18n.changeLanguage(language);

    // Save language preference to database
    try {
      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      await fetch('/api/settings', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          UserId: userId,
          Settings: {
            Language: language
          }
        })
      });
    } catch (error) {
      console.error('Error saving language preference:', error);
    }
  };

  return {
    t,
    i18n,
    changeLanguage,
    currentLanguage: i18n.language,
    isLoading: !i18n.isInitialized
  };
};

export default useTranslation;
