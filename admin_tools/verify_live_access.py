"""운영 Firestore의 읽기 권한만 검사한다. 문서 쓰기나 삭제는 하지 않는다.

Firebase ID 토큰으로 요청해야 실제 Firestore Security Rules가 적용된다.
Google Cloud OAuth/ADC 토큰은 규칙 검증에 쓰지 않는다.
"""

import argparse
import base64
import getpass
import json
import os
import sys
from urllib import error, parse, request


def call(url, payload=None, token=None):
    headers = {}
    if payload is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    req = request.Request(
        url,
        data=json.dumps(payload).encode("utf-8") if payload is not None else None,
        headers=headers,
        method="POST" if payload is not None else "GET",
    )
    try:
        with request.urlopen(req, timeout=20) as response:
            return response.status, response.read()
    except error.HTTPError as exc:
        return exc.code, exc.read()


def inspect_claims(token, project_id):
    middle = token.split(".")[1]
    claims = json.loads(base64.urlsafe_b64decode(middle + "=" * (-len(middle) % 4)))
    if claims.get("aud") != project_id or claims.get("boothStaff") is not True:
        raise RuntimeError("ID 토큰의 프로젝트 또는 boothStaff 클레임이 올바르지 않습니다.")
    print("PASS: 새 ID 토큰에 boothStaff 권한이 있습니다.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-id", required=True)
    args = parser.parse_args()

    api_key = os.environ.get("FIREBASE_WEB_API_KEY") or getpass.getpass("Firebase Web API Key: ")
    email = input("Firebase 스태프 이메일: ").strip()
    password = getpass.getpass("Firebase 스태프 비밀번호(입력 내용 숨김): ")
    auth_url = (
        "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key="
        + parse.quote(api_key, safe="")
    )
    code, body = call(auth_url, {"email": email, "password": password, "returnSecureToken": True})
    if code != 200:
        raise RuntimeError(f"Firebase 로그인 실패: HTTP {code} (비밀번호와 API Key 프로젝트 확인)")
    auth = json.loads(body)
    token = auth["idToken"]
    inspect_claims(token, args.project_id)

    root = (
        "https://firestore.googleapis.com/v1/projects/"
        + parse.quote(args.project_id, safe="")
        + "/databases/(default)/documents"
    )
    checks = [
        ("인증 없는 참가자 목록", root + "/Participants?pageSize=1", None, None, 403),
        ("인증 없는 참가자 단일 조회", root + "/Participants/booth_access_probe_never_created", None, None, 403),
        (
            "인증 없는 참가자 쿼리",
            root + ":runQuery",
            {"structuredQuery": {"from": [{"collectionId": "Participants"}], "limit": 1}},
            None,
            403,
        ),
        ("스태프 통계 조회", root + "/GameState/stats", None, token, 200),
        (
            "스태프 참가자 쿼리",
            root + ":runQuery",
            {"structuredQuery": {"from": [{"collectionId": "Participants"}], "limit": 1}},
            token,
            200,
        ),
        (
            "스태프 이성 후보 쿼리·복합 인덱스",
            root + ":runQuery",
            {
                "structuredQuery": {
                    "from": [{"collectionId": "Participants"}],
                    "where": {
                        "compositeFilter": {
                            "op": "AND",
                            "filters": [
                                {"fieldFilter": {"field": {"fieldPath": "gender"}, "op": "EQUAL", "value": {"stringValue": "여"}}},
                                {"fieldFilter": {"field": {"fieldPath": "isPicked"}, "op": "EQUAL", "value": {"booleanValue": False}}},
                            ],
                        }
                    },
                    "limit": 1,
                }
            },
            token,
            200,
        ),
    ]
    failed = False
    for name, url, payload, bearer, expected in checks:
        actual, _ = call(url, payload, bearer)
        passed = actual == expected
        print(f"{'PASS' if passed else 'FAIL'}: {name}: HTTP {actual} (기대 {expected})")
        failed |= not passed
    if failed:
        raise RuntimeError("읽기 권한 검증 실패. 규칙 배포와 클레임을 확인하세요.")
    print("읽기 권한 검증 완료. 이 도구는 쓰기 권한이나 게임 플레이를 검증하지 않습니다.")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, KeyError, IndexError) as exc:
        print(f"오류: {exc}", file=sys.stderr)
        sys.exit(1)
