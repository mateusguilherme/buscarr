import { useLocation, useNavigate } from 'react-router';
import { ArrowLeftIcon } from './icons';
import { t } from './i18n';

export function BackLink() {
  const navigate = useNavigate();
  const location = useLocation();

  function goBack() {
    if (location.key === 'default') {
      navigate('/');
    } else {
      navigate(-1);
    }
  }

  return (
    <button className="back" onClick={goBack}>
      <ArrowLeftIcon />
      {t.common.back}
    </button>
  );
}
