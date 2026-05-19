export function isInsightHidden(): boolean {
    // hide = true by default
    return process.env.NEXT_PUBLIC_HIDE_INSIGHT !== 'false';
}