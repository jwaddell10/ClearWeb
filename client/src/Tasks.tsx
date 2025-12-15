import React from "react";

type Task = {
	id: number;
	name: string;
	position?: number;
};

type DisplayTasksProps = {
	tasks: Task[];
	setTasks: React.Dispatch<React.SetStateAction<Task[]>>;
	onDelete?: (task: Task) => void;
};

export default function DisplayTasks({
	tasks,
	setTasks,
	onDelete,
}: DisplayTasksProps) {
	const [draggedTask, setDraggedTask] = React.useState<Task | null>(null);

	// ------------------ Gradient helpers ------------------

	const interpolateColor = (
		color1: [number, number, number],
		color2: [number, number, number],
		factor: number
	): string => {
		const result = color1.map((c, i) =>
			Math.round(c + factor * (color2[i] - c))
		) as [number, number, number];
		return `rgb(${result[0]}, ${result[1]}, ${result[2]})`;
	};

	const getWarmGradientColor = (index: number, total: number) => {
		if (total === 1) return "rgb(255, 59, 48)";

		const red: [number, number, number] = [255, 59, 48];
		const orange: [number, number, number] = [255, 149, 0];
		const yellow: [number, number, number] = [255, 204, 0];

		const ratio = index / (total - 1);
		if (ratio <= 0.5) {
			return interpolateColor(red, orange, ratio * 2);
		} else {
			return interpolateColor(orange, yellow, (ratio - 0.5) * 2);
		}
	};

	const getColorForTask = (taskIndex: number, total: number) =>
		getWarmGradientColor(taskIndex, total);

	// ------------------ Drag & Drop ------------------

	const handleDragStart = (task: Task) => setDraggedTask(task);
	const handleDragOver = (e: React.DragEvent) => e.preventDefault();
	const handleDragEnd = () => setDraggedTask(null);

	const handleDrop = async (e: React.DragEvent, targetTask: Task) => {
		e.preventDefault();
		if (!draggedTask || draggedTask.id === targetTask.id) return;

		const draggedIndex = tasks.findIndex((t) => t.id === draggedTask.id);
		const targetIndex = tasks.findIndex((t) => t.id === targetTask.id);

		const newTasks = [...tasks];
		newTasks.splice(draggedIndex, 1);
		newTasks.splice(targetIndex, 0, draggedTask);

		const updatedTasks = newTasks.map((task, index) => ({
			...task,
			position: index,
		}));

		setTasks(updatedTasks);
		setDraggedTask(null);

		// Update backend positions
		await fetch(`${import.meta.env.VITE_API_URL}/api/tasks/reorder`, {
			method: "PUT",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({
				tasks: updatedTasks.map((t) => ({
					id: t.id,
					position: t.position,
				})),
			}),
		});
	};

	const handleDeleteLocal = async (task: Task) => {
		if (onDelete) {
			onDelete(task);
			return;
		}
		await fetch(`${import.meta.env.VITE_API_URL}/api/tasks/${task.id}`, {
			method: "DELETE",
		});
		setTasks((prev) =>
			prev
				.filter((t) => t.id !== task.id)
				.map((t, i) => ({ ...t, position: i }))
		);
	};

	// ------------------ Dark mode ------------------

	const isDarkMode =
		typeof window !== "undefined" &&
		window.matchMedia &&
		window.matchMedia("(prefers-color-scheme: dark)").matches;

	if (!tasks.length) {
		return (
			<div style={{ color: isDarkMode ? "#e0e0e0" : "#333" }}>
				No tasks yet
			</div>
		);
	}

	// ------------------ Render ------------------

	return (
		<ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
			{tasks.map((task, index) => {
				const bgColor =
					draggedTask?.id === task.id
						? isDarkMode
							? "#1e3a5f"
							: "#e3f2fd"
						: getColorForTask(index, tasks.length);

				return (
					<li
						key={task.id ?? `temp-${index}`}
						draggable
						onDragStart={() => handleDragStart(task)}
						onDragOver={handleDragOver}
						onDrop={(e) => handleDrop(e, task)}
						onDragEnd={handleDragEnd}
						style={{
							padding: "16px",
							marginBottom: "8px",
							backgroundColor: bgColor,
							borderRadius: "4px",
							display: "flex",
							alignItems: "center",
							gap: "8px",
							cursor: "move",
							opacity: draggedTask?.id === task.id ? 0.5 : 1,
							color: "#fff",
							border: isDarkMode
								? "1px solid #444"
								: "1px solid #ddd",
						}}
					>
						<span style={{ flex: 1 }}>
							{task.id}: {task.name ?? ""}
						</span>
						<button
							type="button"
							onClick={() => handleDeleteLocal(task)}
							style={{
								padding: "4px 8px",
								cursor: "pointer",
								backgroundColor: isDarkMode ? "#444" : "#fff",
								color: isDarkMode ? "#e0e0e0" : "#333",
								border: isDarkMode
									? "1px solid #666"
									: "1px solid #ccc",
								borderRadius: "4px",
							}}
						>
							Delete
						</button>
					</li>
				);
			})}
		</ul>
	);
}
