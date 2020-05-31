const jsonServer = require("json-server");

const server = jsonServer.create();
const router = jsonServer.router("db.json");
const middlewares = jsonServer.defaults();
const port = parseInt(process.env.PORT, 10) || 3000;

server.use(middlewares);

server.use("/api", router);
server.get("*", (req,res) =>{
  res.sendFile("/public/index.html",{ root: "." });
});
server.listen(port, () => {
  console.log(`JSON Server is running on port ${port}`);
});
