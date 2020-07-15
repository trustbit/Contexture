const jsonServer = require("json-server");
const path = require("path");

const server = jsonServer.create();
const router = jsonServer.router("data/db.json");
const middlewares = jsonServer.defaults();
const port = parseInt(process.env.PORT, 10) || 3000;

server.use(middlewares);

server.use("/api", router);

server.use((req, res, next) =>
  res.sendFile(path.join(__dirname, "public", "index.html"))
);

server.use(function (err, req, res, next) {
  res.status(err.status || 500);
  res.json({
    message: err.message,
    error: process.env.NODE_ENV === "development" ? err : {},
  });

  console.log(err);
});

server.listen(port, () => {
  console.log(`JSON Server is running on port ${port}`);
});
