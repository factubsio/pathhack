import { createSocket } from "dgram";

const PORT = 7331;

function send(msg: object) {
  const udp = createSocket("udp4");
  const buf = Buffer.from(JSON.stringify(msg));
  udp.send(buf, PORT, "127.0.0.1", () => udp.close());
}

const [cmd, ...args] = process.argv.slice(2);

switch (cmd) {
  case "status": {
    const [id, status, ...rest] = args;
    const msg: any = { cmd: "status", id: Number(id), status };
    if (rest.length) msg.notes = rest.join(" ");
    send(msg);
    break;
  }
  case "notes": {
    const [id, ...rest] = args;
    send({ cmd: "notes", id: Number(id), notes: rest.join(" ") });
    break;
  }
  case "add": {
    send({ cmd: "add", text: args.join(" ") });
    break;
  }
  default:
    console.log("usage: bleh-cmd <status|notes|add> ...");
    console.log("  status <id> <open|meat|test|closed> [notes]");
    console.log("  notes <id> <text>");
    console.log("  add <text>");
    break;
}
