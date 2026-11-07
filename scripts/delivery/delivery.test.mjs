import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { sourceContext, migrationJob, approvedRun, component, imageNotFound, render, run, sha } from "./delivery.mjs";
const definitions = JSON.parse(readFileSync(new URL("./components.json", import.meta.url), "utf8"));
const root = fileURLToPath(new URL("../../", import.meta.url));
const digest = "123456789012.dkr.ecr.us-east-1.amazonaws.com/app@sha256:" + "a".repeat(64);
const settings = { kafka: "kafka.shared.svc:9092", issuer: 'https://issuer.example/"quoted"', audience: "oficina:admin" };

test("executa comandos com saida capturada ou herdada e propaga falhas", () => {
  assert.equal(run(process.execPath, ["-e", "process.stdout.write(' result ')"]), "result");
  assert.equal(run(process.execPath, ["-e", "process.exit(0)"], { stdio: "inherit" }), "");
  assert.throws(() => run(process.execPath, ["-e", "process.exit(2)"]));
});

test("rejeita componente desconhecido e SHA invalido", () => {
  assert.throws(() => component("__proto__"));
  assert.throws(() => sha("main"));
  assert.throws(() => sha("a".repeat(39) + ";"));
  assert.equal(sha("a".repeat(40)), "a".repeat(40));
});
test("exige o ultimo CI do commit, concluido com sucesso em main", () => {
  const revision = "b".repeat(40);
  const success = { head_sha: revision, head_branch: "main", event: "push", run_number: 1, status: "completed", conclusion: "success" };
  assert.equal(approvedRun([success], revision), true);
  assert.equal(approvedRun([success, { ...success, run_number: 2, conclusion: "failure" }], revision), false);
  assert.equal(approvedRun([{ ...success, status: "in_progress" }], revision), false);
  assert.equal(approvedRun([{ ...success, event: "pull_request" }], revision), false);
  assert.equal(approvedRun([{ ...success, head_branch: "feature" }], revision), false);
  assert.equal(approvedRun([success], "c".repeat(40)), false);
});
test("somente ImageNotFound permite construir uma tag ausente", () => {
  assert.equal(imageNotFound({ stderr: "An error occurred (ImageNotFoundException)" }), true);
  assert.equal(imageNotFound({ stderr: "An error occurred (AccessDeniedException)" }), false);
  assert.equal(imageNotFound({ stderr: "Unable to locate credentials" }), false);
});
for (const name of Object.keys(definitions)) {
  test(name + ": renderiza sem alterar fontes, preserva HPA/segredos e usa digest", () => {
    const config = component(name);
    const sources = readdirSync(join(root, config.manifests)).filter(f => f.endsWith(".yaml"));
    const before = sources.map(f => readFileSync(join(root, config.manifests, f), "utf8"));
    const docs = render(config, digest, settings, "test-1");
    const deployment = docs.find(d => d.kind === "Deployment");
    assert.equal(deployment.spec.template.spec.containers[0].image, digest);
    assert.equal(deployment.spec.template.metadata.annotations["fiap.tech-challenge/delivery-run"], "test-1");
    assert(docs.every(d => d.metadata.namespace === config.namespace));
    assert(!JSON.stringify(docs).includes("REPLACE_WITH_"));
    if (name !== "frontend") {
      assert(!Object.hasOwn(deployment.spec, "replicas"));
      const db = docs.find(d => d.kind === "ExternalSecret" && d.metadata.name.endsWith("database-credentials"));
      assert.equal(db.spec.data[0].remoteRef.key, "fiap-" + name + "-database-credentials");
      assert.equal(docs.find(d=>d.kind==="HorizontalPodAutoscaler").spec.minReplicas, 1);
      assert.equal(docs.find(d=>d.kind==="ConfigMap").data.Jwt__Issuer, settings.issuer);
    }
    assert.deepEqual(sources.map(f => readFileSync(join(root, config.manifests, f), "utf8")), before);
  });
  test(name + ": rejeita tag mutavel e atualiza anotacao em novo deploy/config", () => {
    const config = component(name);
    assert.throws(() => render(config, "example.com/app:latest", settings, "test"));
    const pod = (docs) => docs.find(d => d.kind === "Deployment").spec.template.metadata.annotations;
    assert.notDeepEqual(pod(render(config, digest, settings, "1")), pod(render(config, digest, settings, "2")));
    if (name !== "frontend") {
      assert.throws(() => render(config, digest, {}, "1"));
      assert.throws(() => render(config, digest, { ...settings, kafka: "broker\ninjection" }, "1"));
      assert.notEqual(pod(render(config, digest, settings, "1"))["fiap.tech-challenge/config-checksum"],
        pod(render(config, digest, { ...settings, audience: "other" }, "1"))["fiap.tech-challenge/config-checksum"]);
    }
  });
}

test("origem e CI usam o repositorio do servico mesmo com chamador central", () => {
  const config = component(Object.keys(definitions)[0]);
  const actual = sourceContext(config, { GITHUB_REPOSITORY: "Maieru/fiap-tech-challenge", GITHUB_SHA: "a".repeat(40), SOURCE_SHA: "b".repeat(40) });
  assert.deepEqual(actual, { repository: config.repository, revision: "b".repeat(40) });
  assert.throws(() => sourceContext(config, { SOURCE_SHA: "main" }));
});
for (const name of Object.keys(definitions).filter(n => ["os", "billing"].includes(n))) {
  test(name + ": migration usa a mesma imagem e somente credencial PostgreSQL", () => {
    const deployment = render(component(name), digest, settings, "test").find(d => d.kind === "Deployment");
    const job = migrationJob(deployment, "migration-test");
    const container = job.spec.template.spec.containers[0];
    assert.equal(container.image, digest);
    assert.deepEqual(container.args, ["--migrate-only"]);
    assert.deepEqual(container.env.map(e => e.name), ["ConnectionStrings__DefaultConnection"]);
    assert.equal(job.spec.template.spec.automountServiceAccountToken, false);
    assert.equal(job.spec.backoffLimit, 0);
  });
}
