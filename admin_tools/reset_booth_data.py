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

# Windows 콘솔 UTF-8 출력 보장
try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

PROJECT_ID = "ludens-booth26-2"

if len(sys.argv) > 1 and sys.argv[1].strip():
    PROJECT_ID = sys.argv[1].strip()

BASE_URL = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents"

def make_request(url, method="GET", data=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Content-Type", "application/json")
    body = json.dumps(data).encode("utf-8") if data else None
    try:
        with urllib.request.urlopen(req, data=body, timeout=10) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode("utf-8")
        print(f"[-] HTTP Error {e.code}: {err_msg}")
        return None
    except Exception as e:
        print(f"[-] Error: {e}")
        return None

def reset_stats():
    print(f"\n[*] GameState/stats 초기화 중...")
    url = f"{BASE_URL}/GameState/stats"
    payload = {
        "fields": {
            "totalDolls": {"integerValue": "100"},
            "totalLegendaryDolls": {"integerValue": "10"},
            "totalRevenue": {"integerValue": "0"},
            "totalRegistrations": {"integerValue": "0"},
            "totalPlays": {"integerValue": "0"},
            "totalSuccesses": {"integerValue": "0"}
        }
    }
    res = make_request(url, method="PATCH", data=payload)
    if res:
        print("[+] 부스 매출 0원, 플레이 수 0회, 인형 재고 100개로 리셋 완료.")

def reset_participants_status():
    print(f"\n[*] Participants 뽑힘 상태(isPicked) 초기화 중...")
    list_url = f"{BASE_URL}/Participants?pageSize=300"
    data = make_request(list_url)
    if not data or "documents" not in data:
        print("[-] 참가자 문서가 없거나 조회 실패.")
        return

    docs = data.get("documents", [])
    print(f"[*] 총 {len(docs)}명의 참가자 상태 리셋 중...")
    for doc in docs:
        doc_path = doc["name"]
        doc_id = doc_path.split("/")[-1]
        patch_url = f"{BASE_URL}/Participants/{doc_id}?updateMask.fieldPaths=isPicked&updateMask.fieldPaths=attempts"
        payload = {
            "fields": {
                "isPicked": {"booleanValue": False},
                "attempts": {"integerValue": "0"}
            }
        }
        make_request(patch_url, method="PATCH", data=payload)

    print(f"[+] 모든 참가자 isPicked=False, attempts=0 리셋 완료.")

if __name__ == "__main__":
    print("="*60)
    print(f"경고: {PROJECT_ID} 의 모든 축제 부스 데이터를 초기화합니다.")
    print("="*60)
    reset_stats()
    reset_participants_status()
    print("\n[+] 초기화 완료! 이제 축제를 시작할 준비가 되었습니다.")
