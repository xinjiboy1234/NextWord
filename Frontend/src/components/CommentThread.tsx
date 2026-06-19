import { useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ArticleComment } from '../types/article'

interface CommentThreadProps {
  articleId: string
  comments: ArticleComment[]
  onAdded: () => void
}

export function CommentThread({ articleId, comments, onAdded }: CommentThreadProps) {
  const [paragraphIndex, setParagraphIndex] = useState(0)
  const [paragraphText, setParagraphText] = useState('')
  const [commentText, setCommentText] = useState('')
  const [requestAiReply, setRequestAiReply] = useState(true)
  const [submitting, setSubmitting] = useState(false)

  async function submit() {
    if (!commentText.trim()) return
    setSubmitting(true)
    try {
      await api.post(endpoints.articleComments(articleId), {
        paragraphIndex,
        paragraphText,
        commentText,
        requestAiReply,
      })
      setCommentText('')
      onAdded()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-4">
      <h2 className="text-lg font-semibold">段落评论</h2>
      <p className="mt-1 text-sm text-neutral-600">点击文中单词后可在此评论该段，AI 可解释（可选）。</p>

      <div className="mt-4 grid gap-3">
        <label className="grid gap-1 text-sm">
          <span>段落索引</span>
          <input
            type="number"
            min={0}
            value={paragraphIndex}
            onChange={(event) => setParagraphIndex(Number(event.target.value))}
            className="h-10 rounded-md border border-neutral-300 px-3"
          />
        </label>
        <label className="grid gap-1 text-sm">
          <span>段落原文（可选）</span>
          <textarea
            value={paragraphText}
            onChange={(event) => setParagraphText(event.target.value)}
            rows={3}
            className="rounded-md border border-neutral-300 px-3 py-2"
          />
        </label>
        <label className="grid gap-1 text-sm">
          <span>你的评论</span>
          <textarea
            value={commentText}
            onChange={(event) => setCommentText(event.target.value)}
            rows={3}
            className="rounded-md border border-neutral-300 px-3 py-2"
          />
        </label>
        <label className="inline-flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={requestAiReply}
            onChange={(event) => setRequestAiReply(event.target.checked)}
          />
          请求 AI 回复
        </label>
        <button
          type="button"
          onClick={() => void submit()}
          disabled={submitting}
          className="inline-flex h-10 w-fit items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white disabled:opacity-60"
        >
          {submitting ? '提交中...' : '发表评论'}
        </button>
      </div>

      <div className="mt-6 space-y-3">
        {comments.length === 0 ? (
          <p className="text-sm text-neutral-600">暂无评论。</p>
        ) : (
          comments.map((comment) => (
            <article key={comment.id} className="rounded-md border border-neutral-200 p-3">
              <p className="text-xs text-neutral-500">段落 {comment.paragraphIndex + 1}</p>
              <p className="mt-1 text-sm font-medium text-neutral-900">{comment.commentText}</p>
              {comment.aiReply && (
                <p className="mt-2 rounded-md bg-emerald-50 p-2 text-sm text-emerald-900">{comment.aiReply}</p>
              )}
            </article>
          ))
        )}
      </div>
    </section>
  )
}
