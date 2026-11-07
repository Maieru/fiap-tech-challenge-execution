import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { appendFileSync, chmodSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { parseAllDocuments, stringify } from "yaml";

const root = fileURLToPath(new URL("../../", import.meta.url));
const components = JSON.parse(readFileSync(new URL("./components.json", import.meta.url), "utf8"));
export function component(name) {
  if (!Object.hasOwn(components, name)) throw new Error("Componente desconhecido.");
  return components[name];
}
export function sha(value) {
  if (!/^[a-f0-9]{40}$/.test(value || "")) throw new Error("Informe um SHA Git completo de 40 caracteres.");
  return value;
}
function required(name) {
  const value = process.env[name]?.trim();
  if (!value || value.includes("REPLACE_WITH_") || /[\r\n]/.test(value)) throw new Error("Configure " + name + ".");
  return value;
}
export function run(command, args, options = {}) {
  const output = execFileSync(command, args, { encoding: "utf8", stdio: ["pipe", "pipe", "pipe"], ...options });
  return typeof output === "string" ? output.trim() : "";
}
function aws(...args) {
  return JSON.parse(run("aws", [...args, "--output", "json", "--no-cli-pager"]));
}
export function approvedRun(runs, revision) {
  const relevant = runs.filter(r => r.head_sha === revision && r.head_branch === "main" &&
    ["push", "workflow_dispatch"].includes(r.event)).sort((a, b) => b.run_number - a.run_number);
  return relevant[0]?.status === "completed" && relevant[0]?.conclusion === "success";
}
export function sourceContext(config, environment = process.env) {
  if (!/^Maieru\/fiap-tech-challenge(?:-execution|-billing)?$/.test(config.repository || '')) throw new Error("Repositorio de origem invalido.");
  return { repository: config.repository, revision: sha(environment.SOURCE_SHA || environment.GITHUB_SHA) };
}
async function verifyCi(config, revision = sourceContext(config).revision) {
  if (process.env.GITHUB_REF !== "refs/heads/main") throw new Error("Entrega permitida somente em main.");
  const repository = sourceContext(config).repository;
  if (run("git", ["rev-parse", "HEAD"], { cwd: root }) !== sourceContext(config).revision) throw new Error("Checkout diverge do SHA de origem.");
  const url = new URL(`https://api.github.com/repos/${repository}/actions/workflows/${config.ci}/runs`);
  url.search = new URLSearchParams({ head_sha: revision, per_page: "100" }).toString();
  const response = await fetch(url, { headers: {
    Authorization: "Bearer " + required("GH_TOKEN"),
    Accept: "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"
  }, signal: AbortSignal.timeout(30000) });
  if (!response.ok) throw new Error("Falha ao consultar CI: HTTP " + response.status);
  if (!approvedRun((await response.json()).workflow_runs, revision)) {
    throw new Error("O ultimo CI de main para " + revision + " precisa estar concluido com sucesso.");
  }
  console.log("CI aprovado: " + revision);
}
function repositoryUri(config) {
  return aws("ecr", "describe-repositories", "--repository-names", config.ecr).repositories[0].repositoryUri;
}
export function imageNotFound(error) {
  return /\(ImageNotFoundException\)/.test(String(error.stderr || ""));
}
function digest(config, revision, optional = false) {
  try {
    const value = aws("ecr", "describe-images", "--repository-name", config.ecr,
      "--image-ids", "imageTag=sha-" + revision).imageDetails[0].imageDigest;
    if (!/^sha256:[a-f0-9]{64}$/.test(value)) throw new Error("Digest ECR invalido.");
    return value;
  } catch (error) {
    if (optional && imageNotFound(error)) return null;
    throw error;
  }
}
function summary(message) {
  console.log(message);
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, message + "\n");
}
function publish(config) {
  const revision = sourceContext(config).revision;
  const uri = repositoryUri(config);
  let hash = digest(config, revision, true);
  if (!hash) {
    const registry = uri.split("/")[0];
    const password = run("aws", ["ecr", "get-login-password"]);
    run("docker", ["login", "--username", "AWS", "--password-stdin", registry], { input: password });
    const tagged = uri + ":sha-" + revision;
    run("docker", ["build", "--label", "org.opencontainers.image.revision=" + revision,
      "--label", "org.opencontainers.image.source=https://github.com/" + sourceContext(config).repository,
      "-f", config.dockerfile, "-t", tagged, config.context], { cwd: root, stdio: "inherit" });
    run("docker", ["push", tagged], { stdio: "inherit" });
    hash = digest(config, revision);
  }
  if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, "image_digest=" + hash + "\nimage_uri=" + uri + "@" + hash + "\n");
  summary("Imagem publicada/verificada: `" + uri + "@" + hash + "` (sha-" + revision + ").");
}
export function render(config, image, settings, deliveryRun) {
  if (!/^[a-zA-Z0-9./_-]+@sha256:[a-f0-9]{64}$/.test(image)) throw new Error("Deploy exige imagem por digest.");
  const documents = readdirSync(join(root, config.manifests)).filter(f => f.endsWith(".yaml")).flatMap(file =>
    parseAllDocuments(readFileSync(join(root, config.manifests, file), "utf8")).map(doc => {
      if (doc.errors.length) throw new Error("YAML invalido: " + file);
      return doc.toJSON();
    }));
  const replacements = {
    REPLACE_WITH_KAFKA_BOOTSTRAP_SERVERS: settings.kafka,
    REPLACE_WITH_JWT_ISSUER: settings.issuer,
    REPLACE_WITH_JWT_AUDIENCE: settings.audience
  };
  for (const doc of documents) {
    if (doc.metadata.namespace !== config.namespace) throw new Error("Namespace fora do escopo.");
    if (doc.kind === "ConfigMap") {
      for (const [key, value] of Object.entries(doc.data)) {
        if (Object.hasOwn(replacements, value)) {
          const replacement = replacements[value];
          if (!replacement || /[\r\n]/.test(replacement) || replacement.includes("REPLACE_WITH_")) {
            throw new Error("Parametro ausente/invalido: " + key);
          }
          doc.data[key] = replacement;
        }
      }
    }
  }
  const configHash = createHash("sha256").update(JSON.stringify(documents.filter(d => d.kind === "ConfigMap"))).digest("hex");
  for (const doc of documents.filter(d => d.kind === "Deployment")) {
    doc.spec.template.spec.containers[0].image = image;
    doc.spec.template.metadata.annotations = {
      ...doc.spec.template.metadata.annotations,
      "fiap.tech-challenge/config-checksum": configHash,
      "fiap.tech-challenge/delivery-run": deliveryRun
    };
  }
  if (documents.some(d => /REPLACE_WITH_|example\.invalid/.test(stringify(d)))) throw new Error("Placeholder restante.");
  return documents;
}
async function kubectlBinary(cluster) {
  const version = aws("eks", "describe-cluster", "--name", cluster).cluster.version;
  if (!/^1\.\d+$/.test(version)) throw new Error("Versao EKS invalida.");
  const get = async url => {
    const response = await fetch(url, { signal: AbortSignal.timeout(60000) });
    if (!response.ok) throw new Error("Download kubectl: HTTP " + response.status);
    return response;
  };
  const release = (await (await get("https://dl.k8s.io/release/stable-" + version + ".txt")).text()).trim();
  if (!new RegExp("^v" + version.replace(".", "\\.") + "\\.\\d+$").test(release)) throw new Error("Versao kubectl divergente.");
  const url = "https://dl.k8s.io/release/" + release + "/bin/linux/amd64/kubectl";
  const data = Buffer.from(await (await get(url)).arrayBuffer());
  const expected = (await (await get(url + ".sha256")).text()).trim();
  if (createHash("sha256").update(data).digest("hex") !== expected) throw new Error("Checksum kubectl invalido.");
  const target = join(mkdtempSync(join(tmpdir(), "fiap-kubectl-")), "kubectl");
  writeFileSync(target, data);
  chmodSync(target, 0o700);
  return target;
}
export function migrationJob(deployment, name) {
  const container = deployment.spec.template.spec.containers[0];
  const connection = container.env.find(e => e.name === "ConnectionStrings__DefaultConnection");
  if (!connection) throw new Error("Migration exige credencial do proprio banco.");
  return { apiVersion: "batch/v1", kind: "Job", metadata: { name, namespace: deployment.metadata.namespace }, spec: {
    backoffLimit: 0, activeDeadlineSeconds: 540, template: { spec: {
      restartPolicy: "Never", automountServiceAccountToken: false,
      securityContext: deployment.spec.template.spec.securityContext,
      volumes: deployment.spec.template.spec.volumes,
      containers: [{ name: "migrate", image: container.image, args: ["--migrate-only"], env: [connection],
        securityContext: container.securityContext, volumeMounts: container.volumeMounts, resources: container.resources }]
    } }
  } };
}
async function deploy(config) {
  const revision = sourceContext(config).revision;
  await verifyCi(config, revision);
  const image = repositoryUri(config) + "@" + digest(config, revision);
  if (process.env.IMAGE_DIGEST && !image.endsWith("@" + process.env.IMAGE_DIGEST)) throw new Error("Digest diverge da publicacao selecionada.");
  const docs = render(config, image, {
    kafka: process.env.KAFKA_BOOTSTRAP_SERVERS?.trim(),
    issuer: process.env.JWT_ISSUER?.trim(), audience: process.env.JWT_AUDIENCE?.trim()
  }, required("DELIVERY_RUN"));
  const cluster = required("EKS_CLUSTER_NAME");
  const cli = await kubectlBinary(cluster);
  const folder = mkdtempSync(join(tmpdir(), "fiap-deploy-"));
  const files = docs.map(doc => {
    const file = join(folder, doc.kind.toLowerCase() + "-" + doc.metadata.name + ".yaml");
    writeFileSync(file, stringify(doc));
    return { doc, file };
  });
  const kubeconfig = join(tmpdir(), basename(folder) + "-kubeconfig");
  run("aws", ["eks", "update-kubeconfig", "--name", cluster, "--region", required("AWS_REGION"), "--kubeconfig", kubeconfig]);
  const k = args => run(cli, ["--kubeconfig", kubeconfig, "-n", config.namespace, ...args]);
  if (docs.some(d => d.kind === "ExternalSecret")) {
    const store = JSON.parse(k(["get", "secretstore", "aws-secrets-store", "-o", "json"]));
    if (!store.status?.conditions?.some(c => c.type === "Ready" && c.status === "True")) {
      throw new Error("SecretStore ainda nao esta pronto.");
    }
  }
  // O diretório contém somente YAML renderizado, sem o kubeconfig.
  k(["apply", "--dry-run=server", "-f", folder]);
  for (const { doc, file } of files.filter(f => f.doc.kind === "ExternalSecret")) {
    k(["apply", "-f", file]);
    const name = doc.metadata.name;
    const previous = JSON.parse(k(["get", "externalsecret", name, "-o", "json"])).status?.refreshTime;
    // refreshTime pode ter precisao de segundos; distinguir uma nova sincronizacao.
    await new Promise(resolve => setTimeout(resolve, 1100));
    k(["annotate", "externalsecret", name, "force-sync=" + Date.now(), "--overwrite"]);
    const deadline = Date.now() + 180000;
    let ready = false;
    while (Date.now() < deadline) {
      const status = JSON.parse(k(["get", "externalsecret", name, "-o", "json"])).status;
      ready = Boolean(status?.refreshTime && status.refreshTime !== previous &&
        status.conditions?.some(c => c.type === "Ready" && c.status === "True"));
      if (ready) break;
      await new Promise(resolve => setTimeout(resolve, 3000));
    }
    if (!ready) throw new Error("Secret nao foi atualizado pelo operador: " + name);
  }
  if (["os", "billing"].includes(process.env.COMPONENT)) {
    const deployment = docs.find(d => d.kind === "Deployment");
    const name = "fiap-" + process.env.COMPONENT + "-migrate-" + revision.slice(0, 12);
    const file = join(tmpdir(), basename(folder) + "-migration.json");
    writeFileSync(file, JSON.stringify(migrationJob(deployment, name)));
    k(["delete", "job", name, "--ignore-not-found=true", "--wait=true"]);
    k(["create", "-f", file]);
    let complete = false;
    for (let attempt = 0; attempt < 120; attempt++) {
      const status = JSON.parse(k(["get", "job", name, "-o", "json"])).status;
      if (status?.conditions?.some(c => c.type === "Failed" && c.status === "True")) break;
      if (status?.succeeded === 1) { complete = true; break; }
      await new Promise(resolve => setTimeout(resolve, 5000));
    }
    console.log(k(["logs", "job/" + name]));
    if (!complete) throw new Error("Migration falhou ou excedeu o prazo: " + name);
    k(["delete", "job", name, "--wait=true"]);
  }
  const priority = { ConfigMap: 0, Service: 1, Deployment: 2, HorizontalPodAutoscaler: 3, Ingress: 4 };
  for (const { file } of files.filter(f => f.doc.kind !== "ExternalSecret")
    .sort((a, b) => priority[a.doc.kind] - priority[b.doc.kind])) {
    k(["apply", "-f", file]);
  }
  const deployment = docs.find(d => d.kind === "Deployment").metadata.name;
  console.log(k(["rollout", "status", "deployment/" + deployment, "--timeout=360s"]));
  summary("Deploy: `" + config.namespace + "/" + deployment + "` → `" + image + "`. Manifestos do commit `" + revision + "`.");
}
async function main() {
  const config = component(required("COMPONENT"));
  switch (process.argv[2]) {
    case "verify-ci": await verifyCi(config); break;
    case "publish": publish(config); break;
    case "deploy": await deploy(config); break;
    default: throw new Error("Use verify-ci, publish ou deploy.");
  }
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
