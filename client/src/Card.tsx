import { useState } from "react";

export default function Card() {
	const [name, setName] = useState("");

	const submit = async (event: { preventDefault: () => void; }) => {
		event.preventDefault();

		try {
			const response = await fetch(`http://localhost:5149/api/hello`, {
				method: "POST",
				headers: {
					"Content-Type": "application/json",
				},
				body: JSON.stringify({name}),
			});

			if (response.ok) {
				console.log(response.body, 'response');
				setName("");
			} else {
				console.error("Form submission faild");
			}
		} catch (error) {
			console.error("Error during form submission", error);
		}
		// const data = formData.get("toDoItem");
		// console.log(data, "data");
	};

	return (
		<form onSubmit={submit}>
			<input
				type="text"
				value={name}
				name="toDoItem"
				onChange={(event) => setName(event.target.value)}
			/>
			<button type="submit">Submit</button>
		</form>
	);
}
