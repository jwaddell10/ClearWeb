import { useState } from "react";

type Message = {
	role: "user" | "ai";
	content: string;
};

export default function AIChat() {
	const [messages, setMessages] = useState<Message[]>([]);
	const [input, setInput] = useState("");
	const [loading, setLoading] = useState(false);

	const isDarkMode =
		typeof window !== "undefined" &&
		window.matchMedia("(prefers-color-scheme: dark)").matches;

	const sendMessage = async () => {
		if (!input.trim()) return;

		const userMessage: Message = { role: "user", content: input };
		setMessages((prev) => [...prev, userMessage]);
		setInput("");
		setLoading(true);

		try {
			const res = await fetch(
				`${import.meta.env.VITE_API_URL}/api/ai/chat`,
				{
					method: "POST",
					headers: { "Content-Type": "application/json" },
					body: JSON.stringify({ prompt: input }),
				}
			);

			const data = await res.json();
            console.log(data, 'data received')

			setMessages((prev) => [
				...prev,
				{ role: "ai", content: data.reply },
			]);
		} catch (error) {
			setMessages((prev) => [
				...prev,
				{ role: "ai", content: "⚠️ AI failed to respond." },
			]);
			console.log(error, "error");
		} finally {
			setLoading(false);
		}
	};

	return (
		<div
			style={{
				marginTop: 24,
				padding: 16,
				borderRadius: 6,
				background: isDarkMode ? "#1e1e1e" : "#f8f8f8",
				border: isDarkMode ? "1px solid #444" : "1px solid #ddd",
			}}
		>
			<h3 style={{ marginTop: 0 }}>🤖 AI Assistant</h3>

			<div
				style={{
					maxHeight: 200,
					overflowY: "auto",
					marginBottom: 12,
				}}
			>
				{messages.length === 0 && (
					<div style={{ opacity: 0.6 }}>
						Ask me about your tasks, priorities, or productivity.
					</div>
				)}

				{messages.map((m, i) => (
					<div
						key={i}
						style={{
							marginBottom: 8,
							padding: 8,
							borderRadius: 4,
							background:
								m.role === "user"
									? "#ffcc00"
									: isDarkMode
									? "#2a2a2a"
									: "#fff",
							color: m.role === "user" ? "#000" : undefined,
						}}
					>
						<strong>{m.role === "user" ? "You" : "AI"}:</strong>{" "}
						{m.content}
					</div>
				))}

				{loading && <div>Thinking…</div>}
			</div>

			<div style={{ display: "flex", gap: 8 }}>
				<input
					value={input}
					onChange={(e) => setInput(e.target.value)}
					placeholder="Ask the AI…"
					style={{
						flex: 1,
						padding: 8,
						fontSize: 14,
					}}
					onKeyDown={(e) => e.key === "Enter" && sendMessage()}
				/>
				<button
					onClick={sendMessage}
					style={{
						padding: "8px 16px",
						cursor: "pointer",
					}}
				>
					Send
				</button>
			</div>
		</div>
	);
}
