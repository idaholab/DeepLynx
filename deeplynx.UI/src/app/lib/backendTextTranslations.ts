export type TranslationMap = Record<string, string>;

export const toBackendTranslationKey = (text: string) =>
  text
    .trim()
    .toUpperCase()
    .replaceAll(" ", "_")
    .replaceAll("/", "_")
    .replaceAll("-", "_");

export const translateBackendText = (
  translationMap: TranslationMap,
  text?: string | null,
) => {
  if (!text) return "";

  const key = toBackendTranslationKey(text);
  return translationMap[key as keyof TranslationMap] ?? text;
};
