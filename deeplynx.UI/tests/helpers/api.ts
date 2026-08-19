import { APIRequestContext } from "../fixtures";

// Shared backend-API helpers used across multiple spec files. Kept separate
// from any one spec so tests aren't duplicating org lookup / record cleanup
// / URL parsing logic.

const BASE_URL = 'http://localhost:5095/api/v1';

type Organization = { id: number; name: string };

// Resolves an org name (e.g. "PW Org A") to its numeric ID (as a string, to
// match how projectId/URLs are handled throughout the specs). We can't
// assume orgId === "1" since fixtures let tests run as accounts scoped to
// arbitrary orgs.
export async function getOrgIdByName(request: APIRequestContext, orgName: string): Promise<string> {
  const res = await request.fetch(`${BASE_URL}/organizations`);
  if (!res.ok()) throw new Error(`Failed to fetch organizations: ${res.status()}`);
  const body = await res.json();
  const orgs: Organization[] = Array.isArray(body) ? body : body.items;
  if (!Array.isArray(orgs)) {
    throw new Error(`Expected array of organizations, got: ${JSON.stringify(body).slice(0, 300)}`);
  }
  const match = orgs.find((org) => org.name === orgName);
  if (!match) throw new Error(`Could not find organization named "${orgName}" in ${JSON.stringify(orgs)}`);
  return String(match.id);
}

// Record page URLs look like: http://localhost:3000/record?recordId=955&projectId=213
export function parseRecordFromUrl(url: string): { recordId: string; projectId: string } | null {
  try {
    const parsed = new URL(url);
    const recordId = parsed.searchParams.get('recordId');
    const projectId = parsed.searchParams.get('projectId');
    if (!recordId || !projectId) return null;
    return { recordId, projectId };
  } catch {
    return null;
  }
}

export async function deleteRecordIfExists(
  request: APIRequestContext,
  record: { recordId: string; projectId: string } | null,
  orgId: string,
): Promise<void> {
  if (!record) return;
  const url = `${BASE_URL}/organizations/${orgId}/projects/${record.projectId}/records/${record.recordId}`;
  const res = await request.delete(url);
  if (!res.ok()) {
    console.warn(`Failed to delete record ${record.recordId}: ${res.status()} ${await res.text()}`);
  }
}