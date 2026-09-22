import json
import time
import urllib.error
import urllib.request

BASE_URL = "http://localhost:8080"


def request(path, payload=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(
        BASE_URL + path,
        data=None if payload is None else json.dumps(payload).encode(),
        headers=headers,
    )
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def wait_until_healthy():
    for _ in range(30):
        try:
            if request("/health")[0] == 200:
                return
        except (urllib.error.URLError, TimeoutError, ConnectionError):
            pass
        time.sleep(2)
    raise RuntimeError("API did not become healthy")


def main():
    wait_until_healthy()
    assert request("/api/v1/admin/auth/token", {"key": "local-admin-key-change-me"})[0] == 400
    assert request("/api/v1/admin/auth/token", {"username": "platform"})[0] == 400
    assert request("/api/v1/admin/auth/token", {"username": "platform", "password": "x" * 129})[0] == 400
    assert request("/api/v1/admin/auth/token", {"username": "platform", "password": "wrong"})[0] == 401

    status, body = request("/api/v1/admin/auth/token", {"username": "PLATFORM", "password": "platform"})
    assert status == 200, "Administrator login failed"
    token = json.loads(body)["access_token"]
    assert request("/api/v1/admin/tenants", token=token)[0] == 200
    assert request("/api/v1/overview", token=token)[0] == 403
    print("Administrator password login and tenant separation verified.")


if __name__ == "__main__":
    main()
