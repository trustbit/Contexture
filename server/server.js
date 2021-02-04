const jsonServer = require("json-server");
const path = require("path");
const fs = require("fs");
const mkdirp = require("mkdirp");

const basePath = process.env.NODE_ENV == "production" ? "/data": "./";
const dbPath = path.join(basePath, "db.json");

if (!fs.existsSync(basePath)) {
  mkdirp.sync(basePath);

  console.log(`Created db directory ${basePath}. In case you're running this container in docker you should map a docker volume to ${basePath} to persist data.`);
}

if (!fs.existsSync(dbPath)) {
  try {
    fs.writeFileSync(dbPath, JSON.stringify({
      "domains": [],
      "boundedContexts": [],
      "collaborations": []
    }), { encoding: 'utf-8'});
  } catch(e) {
    console.log(`${dbPath} already exists`);
  }
} else {
  const fileContent = fs.readFileSync(dbPath, { encoding: 'utf-8'});
  if(fileContent){
    // this is the current way to do/execute 'DB' migrations
    const jsonFileContent = JSON.parse(fileContent);
    if(!jsonFileContent.collaborations) {
      console.log(`Creating empty collaborations`);
      jsonFileContent.collaborations = [];
    }
    fs.writeFileSync(dbPath, JSON.stringify (jsonFileContent));
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
