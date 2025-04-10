import { formatDistanceToNow, format } from "date-fns";
import { enGB } from "date-fns/locale";

export const useDateUtils = (locale = enGB) => {
  const toRelativeTime = (isoDate: string) => {
    if (!isoDate) return "";
    return formatDistanceToNow(new Date(isoDate), { addSuffix: true, locale });
  };

  const toFormattedDate = (isoDate: string, dateFormat = "dd MMM yyyy HH:mm") => {
    if (!isoDate) return "";
    return format(new Date(isoDate), dateFormat, { locale });
  };

  return {
    toRelativeTime,
    toFormattedDate,
  };
};
