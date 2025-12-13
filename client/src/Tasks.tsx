import { useEffect, useState } from "react";

type Task = {
	id: number;
	name: string;
};

export default function DisplayTasks() {
	const [tasks, setTasks] = useState<Task[] | null>(null);
	console.log(tasks, "tasks");
	useEffect(() => {
		const fetchData = async () => {
			const response = await fetch(`http://localhost:5149/tasks`);
			const data = await response.json();
			setTasks(data);
		};

		fetchData();
	}, []);

	const handleDelete = async (task) => {
		console.log(JSON.stringify({ task }), "task");
		const response = await fetch(
			`http://localhost:5149/api/tasks/${task.id}`,
			{
				method: "DELETE",
			}
		);

		const data = await response.json();
		console.log(data, "data from backend");
	};

	const changePriority = async (event, task) => {
		if (event.target.textContent === "+") {
			const response = await fetch(
				`http://localhost:5149/api/tasks/update/${task.id}`,
				{
					method: "PUT",
					headers: { "Content-type": "appliciation/json" },
					body: JSON.stringify({ task }),
				}
			);

			const data = await response.json();
			console.log(data, "data");
		}
	};

	return (
		<>
			{tasks?.map((task: Task) => (
				<li>
					{task.id}: {task.name}
					<button
						type="button"
						onClick={() => {
							handleDelete(task);
						}}
					>
						Delete
					</button>
					<button
						type="button"
						onClick={(event) => {
							changePriority(event, task);
						}}
					>
						+
					</button>
					<button
						type="button"
						onClick={(event) => {
							changePriority(event, task);
						}}
					>
						-
					</button>
				</li>
			))}
		</>
	);
}
