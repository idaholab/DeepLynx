import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";

interface InsightMarkdownMessageProps {
    content: string;
}

function normalizeInsightMarkdown(content: string): string {
    let normalized = content;

    // Only unwrap when the ENTIRE response starts with ```markdown or ```md.
    // Avoids changing legitimate markdown code examples that appear later in the response.
    normalized = normalized.replace(
        /^(\s*)```(?:markdown|md)\s*\r?\n/i,
        "$1",
    );

    // Convert fenced LaTeX blocks into display math so remark-math/KaTeX can render them.
    normalized = normalized.replace(
        /```(?:latex|tex)\s*\r?\n([\s\S]*?)\r?\n```/gi,
        (_match, math) => `$$\n${math.trim()}\n$$`,
    );

    return normalized;
}

export default function InsightMarkdownMessage({
    content,
}: InsightMarkdownMessageProps) {
    const normalizedContent = normalizeInsightMarkdown(content);
    
    return (
        <div className="insight-markdown-message">
            <ReactMarkdown
                remarkPlugins={[remarkGfm, remarkMath]}
                rehypePlugins={[rehypeKatex]}
            >
                {normalizedContent}
            </ReactMarkdown>    
        </div>
    );
}