"""Google Cloud Shell에서 키 파일 없이 스태프 커스텀 클레임을 설정한다.

기본 동작은 대상 조회와 변경 내용 미리보기이며 --apply 때만 변경한다.
Cloud Shell에 로그인한 Google 계정에 Firebase Authentication 사용자 조회/수정
IAM 권한이 있어야 한다. 계정 비밀번호나 액세스 토큰은 출력하지 않는다.
"""

import argparse
import json
import subprocess
import sys
from urllib import error, parse, request


def gcloud(*args):
    return subprocess.check_output(
        ["gcloud", *args], text=True, stderr=subprocess.DEVNULL
    ).strip()


def post(project_id, path, token, payload):
    url = (
        "https://identitytoolkit.googleapis.com/v1/projects/"
        + parse.quote(project_id, safe="")
        + "/accounts:"
        + path
    )
    req = request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": "Bearer " + token,
            "Content-Type": "application/json",
            # 사용자 OAuth 토큰을 쓰는 REST 호출은 할당량 프로젝트를 명시해야 한다.
            "x-goog-user-project": project_id,
        },
        method="POST",
    )
    try:
        with request.urlopen(req, timeout=20) as response:
            return json.load(response)
    except error.HTTPError as exc:
        # API 오류만 표시하고 요청 헤더의 액세스 토큰은 출력하지 않는다.
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Firebase API HTTP {exc.code}: {detail}") from None


def lookup(project_id, uid, token):
    users = post(project_id, "lookup", token, {"localId": [uid]}).get("users", [])
    if len(users) != 1 or users[0].get("localId") != uid:
        raise RuntimeError("지정한 UID의 사용자를 프로젝트에서 찾지 못했습니다.")
    return users[0]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-id", required=True)
    parser.add_argument("--uid", required=True)
    parser.add_argument("--admin", action="store_true", help="재고 관리 boothAdmin 권한도 부여")
    parser.add_argument("--apply", action="store_true", help="미리보기 후 실제 변경")
    args = parser.parse_args()

    active_project = gcloud("config", "get-value", "project")
    if active_project != args.project_id:
        raise RuntimeError(
            f"Cloud Shell 활성 프로젝트가 {active_project!r}입니다. "
            f"{args.project_id!r}로 전환한 뒤 다시 실행하세요."
        )

    token = gcloud("auth", "print-access-token")
    user = lookup(args.project_id, args.uid, token)
    if user.get("disabled"):
        raise RuntimeError("대상 Firebase Authentication 계정이 비활성화되어 있습니다.")
    current = json.loads(user.get("customAttributes") or "{}")
    if not isinstance(current, dict):
        raise RuntimeError("기존 커스텀 클레임 형식이 올바르지 않습니다.")
    updated = dict(current)
    updated["boothStaff"] = True
    if args.admin:
        updated["boothAdmin"] = True

    print(f"project={args.project_id}")
    print(f"uid={user['localId']}")
    print(f"email={user.get('email', '(없음)')}")
    print(f"기존 클레임={json.dumps(current, ensure_ascii=False, sort_keys=True)}")
    print(f"설정할 클레임={json.dumps(updated, ensure_ascii=False, sort_keys=True)}")
    if not args.apply:
        print("미리보기만 했습니다. 대상이 맞으면 같은 명령에 --apply를 붙이세요.")
        return

    post(
        args.project_id,
        "update",
        token,
        {"localId": args.uid, "customAttributes": json.dumps(updated, separators=(",", ":"))},
    )
    verified = lookup(args.project_id, args.uid, token)
    saved = json.loads(verified.get("customAttributes") or "{}")
    if saved != updated:
        raise RuntimeError("변경 요청 후 조회한 클레임이 예상 값과 다릅니다. 다시 확인하세요.")
    print("클레임 저장 및 재조회 확인 완료. 게임에서 로그아웃 후 다시 로그인하세요.")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.CalledProcessError, ValueError) as exc:
        print(f"오류: {exc}", file=sys.stderr)
        sys.exit(1)
