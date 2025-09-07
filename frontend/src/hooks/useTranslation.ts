import { useTranslation as useI18nTranslation } from 'react-i18next';
import useEffectiveUser from './useEffectiveUser';

export const useTranslation = () => {
  const { t, i18n } = useI18nTranslation();
  const eff = useEffectiveUser();

  const changeLanguage = async (language: string) => {
    await i18n.changeLanguage(language);

    // Save language preference to database
    try {
      const userId = eff.userId || '00000000-0000-0000-0000-000000000001';

      await fetch('/api/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...eff.headers },
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
