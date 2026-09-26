#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
[루덴스 축제 부스] 데이터 리셋 스크립트
축제 전날/당일 아침 사전 테스트로 쌓인 점수, 매출, 시도 횟수를 0으로 리셋하고,
모든 참가자의 뽑힘 상태(isPicked)를 초기화(False)합니다.
"""

import sys
import json
import urllib.request
import urllib.error
from _auth import authorization_header
import urllib.parse

# Windows 콘솔 UTF-8 출력 보장
try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

PROJECT_ID = "ludens-booth26-2"

if len(sys.argv) > 1 and sys.argv[1].strip() and not sys.argv[1].startswith("--"):
    PROJECT_ID = sys.argv[1].strip()

BASE_URL = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents"
INITIAL_DOLLS = None
INITIAL_LEGENDARY = None

def make_request(url, method="GET", data=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", authorization_header(PROJECT_ID))
    body = json.dumps(data).encode("utf-8") if data else None
    try:
        with urllib.request.urlopen(req, data=body, timeout=10) as response:
            raw = response.read().decode("utf-8")
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode("utf-8")
        print(f"[-] HTTP Error {e.code}: {err_msg}")
        return None
    except Exception as e:
        print(f"[-] Error: {e}")
        return None

def list_all_documents(collection):
    documents = []
    page_token = None
    while True:
        query = {"pageSize": "300"}
        if page_token:
            query["pageToken"] = page_token
        data = make_request(f"{BASE_URL}/{collection}?{urllib.parse.urlencode(query)}")
        if data is None:
            raise RuntimeError(f"{collection} 목록 조회 실패")
        documents.extend(data.get("documents", []))
        page_token = data.get("nextPageToken")
        if not page_token:
            return documents

def reset_stats(registration_count):
    print(f"\n[*] GameState/stats 초기화 중...")
    url = f"{BASE_URL}/GameState/stats"
    payload = {
        "fields": {
            "totalDolls": {"integerValue": str(INITIAL_DOLLS)},
            "totalLegendaryDolls": {"integerValue": str(INITIAL_LEGENDARY)},
            "totalRevenue": {"integerValue": "0"},
            "totalRegistrations": {"integerValue": str(registration_count)},
            "totalPlays": {"integerValue": "0"},
            "totalSuccesses": {"integerValue": "0"}
        }
    }
    res = make_request(url, method="PATCH", data=payload)
    if res:
        print(f"[+] 매출/플레이를 초기화하고 현재 참가자 {registration_count}명을 반영했습니다.")
        return
    raise RuntimeError("GameState/stats 초기화 실패")

def reset_participants_status(docs):
    print(f"\n[*] Participants 뽑힘 상태(isPicked) 초기화 중...")
    print(f"[*] 총 {len(docs)}명의 참가자 상태 리셋 중...")
    for doc in docs:
        doc_path = doc["name"]
        doc_id = doc_path.split("/")[-1]
        encoded_id = urllib.parse.quote(doc_id, safe="")
        patch_url = f"{BASE_URL}/Participants/{encoded_id}?updateMask.fieldPaths=isPicked&updateMask.fieldPaths=attempts"
        payload = {
            "fields": {
                "isPicked": {"booleanValue": False},
                "attempts": {"integerValue": "0"}
            }
        }
        if make_request(patch_url, method="PATCH", data=payload) is None:
            raise RuntimeError(f"참가자 상태 초기화 실패: {doc_id}")

    print(f"[+] 모든 참가자 isPicked=False, attempts=0 리셋 완료.")

def clear_profile_claims():
    """새 행사에서 프로필을 다시 추첨할 수 있도록 선점 문서만 제거합니다.

    GameRounds/MatchResults/InventoryChanges는 재전송 중복 방지 영수증이므로 유지합니다.
    """
    docs = list_all_documents("ProfileClaims")
    print(f"\n[*] ProfileClaims {len(docs)}개 삭제 중...")
    for doc in docs:
        doc_id = doc["name"].split("/")[-1]
        if make_request(f"{BASE_URL}/ProfileClaims/{urllib.parse.quote(doc_id, safe='')}", method="DELETE") is None:
            raise RuntimeError(f"프로필 선점 초기화 실패: {doc_id}")
    print("[+] 프로필 선점 상태 초기화 완료.")

if __name__ == "__main__":
    if "--apply" not in sys.argv or "--dolls" not in sys.argv or "--legendary" not in sys.argv:
        print("미리보기: 변경 없음. 실행하려면 --apply --dolls 실제수량 --legendary 실제수량을 지정하세요.")
        sys.exit(2)
    try:
        INITIAL_DOLLS = int(sys.argv[sys.argv.index("--dolls") + 1])
        INITIAL_LEGENDARY = int(sys.argv[sys.argv.index("--legendary") + 1])
        if INITIAL_DOLLS < 0 or INITIAL_LEGENDARY < 0:
            raise ValueError("재고는 음수가 될 수 없습니다")
    except (ValueError, IndexError) as exc:
        print(f"재고 인자를 확인해 주세요: {exc}")
        sys.exit(2)
    print("="*60)
    print(f"경고: {PROJECT_ID} 의 모든 축제 부스 데이터를 초기화합니다.")
    print("="*60)
    print("주의: 초기화 중에는 사격 게임과 같은 DB를 쓰는 다른 앱을 모두 종료하세요.")
    try:
        participant_docs = list_all_documents("Participants")
        reset_participants_status(participant_docs)
        clear_profile_claims()
        reset_stats(len(participant_docs))
        print("\n[+] 초기화 완료! 이제 축제를 시작할 준비가 되었습니다.")
    except RuntimeError as exc:
        print(f"\n[-] 초기화 중단: {exc}")
        sys.exit(1)
