// A worker for the subprocess tests: it echoes, exits, or hangs, as the request says.
process.on("message", (request) => {
  if (request.mode === "echo") process.send({ echoed: request.value }, () => process.exit(0));
  else if (request.mode === "exit") process.exit(3);
  // "hang": never reply, so the hang guard or a cancellation has to stop it.
});