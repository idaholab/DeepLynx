import requests
import json
import sys
import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

URL = f'{os.getenv("MCP_SERVER_URL")}/mcp'
TOKEN = os.getenv("PYTHON_TEST_CLIENT_TOKEN")

if not URL or not TOKEN:
    print("Error: MCP_SERVER_URL and PYTHON_TEST_CLIENT_TOKEN must be set in .env file", file=sys.stderr)
    sys.exit(1)

IS_ORIGIN = "false"
PAGE = 1

def call_mcp(method, params={}):
    """Make a JSON-RPC call to the MCP endpoint."""
    payload = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": method,
        "params": params or {}
    }
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {TOKEN}"
    }

    resp = requests.post(URL, headers=headers, json=payload)
    resp.raise_for_status()

    data = parse_sse_response(resp.text)

    # Check for JSON-RPC level error
    if data.get("error"):
        print(f"\nJSON-RPC Error: {data['error']}", file=sys.stderr)
        sys.exit(1)
    
    result = data.get("result")
    
    # Check for MCP tool-level error (isError in the result)
    if result and result.get("isError"):
        error_msg = "Unknown error"
        if result.get("content"):
            error_msg = result["content"][0].get("text", error_msg)
        print(f"\nMCP Tool Error: {error_msg}", file=sys.stderr)
        sys.exit(1)

    return result

def parse_sse_response(text):
    """Extract JSON from SSE-formatted response."""
    for line in text.strip().split('\n'):
        if line.startswith('data: '):
            json_str = line[6:]  # Remove 'data: ' prefix
            return json.loads(json_str)
    raise ValueError(f"No data field found in SSE response: {text}")

def main():
    # list available tools
    print("\n=== Listing tools... ===")
    tools = call_mcp("tools/list")
    # print(json.dumps(tools, indent=2))
    print("\n=== Tools listed! ===")

    print("\n=== Listing organizations... ===")
    orgs_result = call_mcp("tools/call", {"name": "get_all_organizations", "arguments": {}})
    # print(json.dumps(orgs_result, indent=2))
    print("\n=== Organizations listed! ===")

    # Parse the nested JSON string from the response
    orgs_text = orgs_result["content"][0]["text"]
    orgs = json.loads(orgs_text)
    
    if not orgs:
        print("No organizations found. Terminating...")
        sys.exit(0)
    
    # Get the first organization's ID
    org_id = orgs[0]["Id"]
    org_name = orgs[0]["Name"]
    print(f"\n===\nUsing first organization found:\n{org_name} (ID: {org_id}) to list projects\n===")
    
    print("\n=== Listing projects... ===")
    proj_result = call_mcp("tools/call", {
        "name": "get_all_projects",
        "arguments": {"organizationId": org_id}
    })
    # print(json.dumps(proj_result, indent=2))
    print("\n=== Projects listed! ===")

    # Parse the nested JSON string from the response
    proj_text = proj_result["content"][0]["text"]
    proj = json.loads(proj_text)
    
    if not proj:
        print("No projects found. Terminating...")
        sys.exit(0)
    
    # Get the first project's ID
    proj_id = proj[0]["Id"]
    proj_name = proj[0]["Name"]
    print(f"\n===\nUsing first project found:\n{proj_name} (ID: {proj_id}) to list records\n===")
    
    print("\n=== Listing records... ===")
    rec_result = call_mcp("tools/call", {
        "name": "get_all_records",
        "arguments": {"organizationId": org_id, "projectId": proj_id}
    })
    # print(json.dumps(rec_result, indent=2))
    print("\n=== Records listed! ===")

    # Parse the nested JSON string from the response
    rec_text = rec_result["content"][0]["text"]
    rec = json.loads(rec_text)
    
    if not rec:
        print("No recects found. Terminating...")
        sys.exit(0)
    
    # Get the first recect's ID
    rec_id = rec[0]["Id"]
    rec_name = rec[0]["Name"]
    print(f"\n===\nUsing first project found:\n{rec_name} (ID: {rec_id}) to list graph\n===")

    print("\n=== Fetching graph... ===")
    graph_result = call_mcp("tools/call", {
        "name": "get_related_records",
        "arguments": {"organizationId": org_id, "projectId": proj_id, "recordId": rec_id, "isOrigin": IS_ORIGIN, "page": PAGE}
    })
    # print(json.dumps(graph_result, indent=2))
    print("\n=== Graph fetched! ===")



if __name__ == "__main__":
    main()