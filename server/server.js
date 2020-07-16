const jsonServer = require("json-server");
const path = require("path");
const fs = require("fs");

const dbPath = process.env.NODE_ENV == "production" ? "/data/db.json" : "db.json";

if (!fs.existsSync(dbPath)) {
  try {
    fs.writeFileSync(dbPath, JSON.stringify({
      "domains": [],
      "bccs": []
    }), { encoding: 'utf-8'});
  } catch(e) {
    console.log(`${dbPath} already exists`);
  }
}

const server = jsonServer.create();
const router = jsonServer.router(dbPath);

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
