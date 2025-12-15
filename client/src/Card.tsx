import { useEffect, useState, useCallback } from "react";
import DisplayTasks from "./Tasks";

type Task = {
	id: number;
	name: string;
	position?: number;
};

function normalizeTask(input: any, indexFallback?: number): Task {
	// Accept both lower/upper case keys from backend
	const id = input.id ?? input.Id;
	const name = input.name ?? input.Name ?? "";
	const position = input.position ?? input.Position ?? indexFallback;

	return { id, name, position };
}

export default function Card() {
	const [tasks, setTasks] = useState<Task[]>([]);
	const [name, setName] = useState("");

	const fetchTasks = useCallback(async () => {
		const response = await fetch(`${import.meta.env.VITE_API_URL}/tasks`);
		const data = await response.json();

		// The GET /tasks returns a list of dictionaries; normalize each
		const normalized: Task[] = (Array.isArray(data) ? data : [])
			.map((row: any, idx: number) => normalizeTask(row, idx))
			.sort((a, b) => (a.position ?? a.id) - (b.position ?? b.id));

		setTasks(normalized);
	}, []);

	useEffect(() => {
		fetchTasks();
	}, [fetchTasks]);

	const submit = async (event: React.FormEvent) => {
		event.preventDefault();
		if (!name.trim()) return;

		try {
			const response = await fetch(
				`${import.meta.env.VITE_API_URL}/tasks`,
				{
					method: "POST",
					headers: { "Content-Type": "application/json" },
					body: JSON.stringify({ name }),
				}
			);

			if (!response.ok) {
				console.error("Form submission failed");
				return;
			}

			// Backend returns { Id, Name, Position }
			const raw = await response.json();
			const newTask = normalizeTask(raw);

			// Append locally; ensure unique id and position
			setTasks((prev) => {
				const appended = [...prev, newTask];
				// Reindex positions to ensure gradient consistency at the bottom
				return appended.map((t, i) => ({ ...t, position: i }));
			});

			setName("");
		} catch (error) {
			console.error("Error during form submission", error);
		}
	};

	const handleDelete = async (task: Task) => {
		try {
			const res = await fetch(
				`${import.meta.env.VITE_API_URL}/api/tasks/${task.id}`,
				{
					method: "DELETE",
				}
			);

			if (!res.ok) {
				console.error("Error deleting task");
				return;
			}

			setTasks((prev) =>
				prev
					.filter((t) => t.id !== task.id)
					.map((t, i) => ({ ...t, position: i }))
			);
		} catch (error) {
			console.error("Error deleting task", error);
		}
	};

	return (
		<>
			<DisplayTasks
				tasks={tasks}
				setTasks={setTasks}
				onDelete={handleDelete}
			/>
			<form onSubmit={submit} style={{ marginTop: 16 }}>
				<input
					type="text"
					value={name}
					name="toDoItem"
					onChange={(e) => setName(e.target.value)}
					required
					placeholder="Add new task"
					style={{ padding: "8px", fontSize: "16px", width: "70%" }}
				/>
				<button
					type="submit"
					style={{
						padding: "8px 16px",
						marginLeft: "8px",
						fontSize: "16px",
						cursor: "pointer",
					}}
				>
					Add Task
				</button>
			</form>
		</>
	);
}
