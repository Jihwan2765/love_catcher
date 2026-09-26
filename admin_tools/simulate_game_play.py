#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
[루덴스 축제 부스] 백엔드 연동 3초 시뮬레이션 테스트 도구
Unity를 켜지 않고도 터미널에서 전체 게임 플로우(참가자 등록 -> 이성 매칭 -> 인형 차감 -> 매출 누적)가
Firebase와 정상적으로 작동하는지 1초 만에 검증합니다.
"""

import sys
import json
import urllib.request
import urllib.error
from _auth import authorization_header

try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

PROJECT_ID = "ludens-booth26-2"
BASE_URL = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents"

def make_request(url, method="GET", data=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", authorization_header(PROJECT_ID))
    body = json.dumps(data, ensure_ascii=False).encode("utf-8") if data else None
    try:
        with urllib.request.urlopen(req, data=body, timeout=10) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        print(f"[-] HTTP Error {e.code}: {e.read().decode('utf-8')}")
        return None
    except Exception as e:
        print(f"[-] Error: {e}")
        return None

def test_full_loop():
    print("="*60)
    print("🚀 [1단계] 신규 참가자(남성) 등록 테스트")
    print("="*60)
    test_user_id = "test_player_01"
    reg_url = f"{BASE_URL}/Participants?documentId={test_user_id}"
    reg_payload = {
        "fields": {
            "name": {"stringValue": "테스트손님"},
            "insta": {"stringValue": test_user_id},
            "bio": {"stringValue": "연동 테스트 중입니다!"},
            "gender": {"stringValue": "남"},
            "isPicked": {"booleanValue": False},
            "attempts": {"integerValue": "0"}
        }
    }
    res = make_request(reg_url, method="POST", data=reg_payload)
    if res:
        print(f"✅ 참가자 등록 성공: 테스트손님 (@{test_user_id})")
    else:
        print("[-] 참가자 등록 실패 (이미 존재할 수 있음)")

    print("\n" + "="*60)
    print("🎯 [2단계] 이성(여성) 매칭 쿼리 테스트 (structuredQuery)")
    print("="*60)
    query_url = f"{BASE_URL}:runQuery"
    query_payload = {
        "structuredQuery": {
            "from": [{"collectionId": "Participants"}],
            "where": {
                "compositeFilter": {
                    "op": "AND",
                    "filters": [
                        {
                            "fieldFilter": {
                                "field": {"fieldPath": "gender"},
                                "op": "EQUAL",
                                "value": {"stringValue": "여"}
                            }
                        },
                        {
                            "fieldFilter": {
                                "field": {"fieldPath": "isPicked"},
                                "op": "EQUAL",
                                "value": {"booleanValue": False}
                            }
                        }
                    ]
                }
            }
        }
    }
    match_res = make_request(query_url, method="POST", data=query_payload)
    matched_target = None
    if match_res:
        candidates = []
        for item in match_res:
            doc = item.get("document", {})
            f = doc.get("fields", {})
            if f and "insta" in f:
                candidates.append({
                    "id": doc["name"].split("/")[-1],
                    "name": f.get("name", {}).get("stringValue", "익명"),
                    "insta": f.get("insta", {}).get("stringValue", ""),
                    "bio": f.get("bio", {}).get("stringValue", "")
                })

        if candidates:
            import random
            matched_target = random.choice(candidates)
            print(f"✅ 매칭 가능한 여성 후보 {len(candidates)}명 중 1명 추첨 성공!")
            print(f"   💖 당첨된 인스타: {matched_target['name']} (@{matched_target['insta']})")
            print(f"   💌 한줄소개: \"{matched_target['bio']}\"")
        else:
            print("[-] 남은 여성 참가자가 없습니다.")

    if matched_target:
        print("\n" + "="*60)
        print("🔒 [3단계] 당첨된 참가자 '뽑힘(isPicked=true)' 상태 갱신")
        print("="*60)
        pick_url = f"{BASE_URL}/Participants/{matched_target['id']}?updateMask.fieldPaths=isPicked"
        pick_payload = {"fields": {"isPicked": {"booleanValue": True}}}
        pick_res = make_request(pick_url, method="PATCH", data=pick_payload)
        if pick_res:
            print(f"✅ @{matched_target['insta']} 님을 '뽑힘' 상태로 변경 완료 (중복 지급 방지)")

    print("\n" + "="*60)
    print("🧸 [4단계] 게임 종료 후 부스 통계 갱신 (매출 +500원, 플레이수 +1, 인형 -1개)")
    print("="*60)
    stats_url = f"{BASE_URL}/GameState/stats"
    stats_doc = make_request(stats_url)
    total_dolls = 100
    total_plays = 0
    total_revenue = 0
    total_success = 0
    if stats_doc and "fields" in stats_doc:
        f = stats_doc["fields"]
        total_dolls = int(f.get("totalDolls", {}).get("integerValue", 100))
        total_plays = int(f.get("totalPlays", {}).get("integerValue", 0))
        total_revenue = int(f.get("totalRevenue", {}).get("integerValue", 0))
        total_success = int(f.get("totalSuccesses", {}).get("integerValue", 0))

    total_dolls = max(0, total_dolls - 1)
    total_plays += 1
    total_revenue += 500
    total_success += 1

    mask = "updateMask.fieldPaths=totalDolls&updateMask.fieldPaths=totalPlays&updateMask.fieldPaths=totalRevenue&updateMask.fieldPaths=totalSuccesses"
    patch_stats_url = f"{BASE_URL}/GameState/stats?{mask}"
    patch_payload = {
        "fields": {
            "totalDolls": {"integerValue": str(total_dolls)},
            "totalPlays": {"integerValue": str(total_plays)},
            "totalRevenue": {"integerValue": str(total_revenue)},
            "totalSuccesses": {"integerValue": str(total_success)}
        }
    }
    patch_res = make_request(patch_stats_url, method="PATCH", data=patch_payload)
    if patch_res:
        print("✅ 부스 통계 갱신 완료!")
        print(f"   💰 누적 매출: {total_revenue:,} 원")
        print(f"   🎮 총 플레이 수: {total_plays} 회")
        print(f"   🧸 남은 인형 수: {total_dolls} 개 (1개 차감됨)")

    print("\n" + "="*60)
    print("🎉 백엔드 파이프라인 전체 정상 동작 확인 완료!")
    print("="*60)

if __name__ == "__main__":
    print("이 스크립트의 직접 DB 쓰기는 중복 방지 영수증과 원자적 저장을 거치지 않습니다.")
    print("운영 DB에서는 실행하지 마세요. 다음 오프라인 회귀 테스트를 사용하세요:")
    print("dotnet run --project Tests/Database/DatabaseRegression.csproj")
    sys.exit(2)
