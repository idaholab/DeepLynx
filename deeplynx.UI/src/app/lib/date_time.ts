export const toUtcIsoIfNaive = (input: string): string => {
  if (/([zZ]|[+-]\d{2}:\d{2})$/.test(input)) return input;
  return `${input}Z`;
};

export const formatLocalDateTime = (dateString: string): string => {
  const normalized = toUtcIsoIfNaive(dateString);
  const date = new Date(normalized);

  return date.toLocaleString(undefined, {
    month: "long",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "numeric",
    hour12: true,
    timeZoneName: "short",
  });
};
