#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
[루덴스 축제 부스] 행사 마감 통계 확인 및 개인정보 파기 도구
- 부스 총 매출, 총 플레이 수, 남은 인형 수량 집계
- (선택) --purge 옵션 사용 시 축제 종료 후 참가자 인스타 개인정보 일괄 삭제
"""

import sys
import json
import urllib.request
import urllib.error
import urllib.parse
from _auth import authorization_header

# Windows 콘솔 UTF-8 출력 보장
try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

PROJECT_ID = "ludens-booth26-2"

if len(sys.argv) > 1 and not sys.argv[1].startswith("--"):
    PROJECT_ID = sys.argv[1].strip()

BASE_URL = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents"

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
    result = []
    token = None
    while True:
        query = {"pageSize": "300"}
        if token:
            query["pageToken"] = token
        page = make_request(f"{BASE_URL}/{collection}?{urllib.parse.urlencode(query)}")
        if page is None:
            raise RuntimeError(f"{collection} 조회 실패")
        result.extend(page.get("documents", []))
        token = page.get("nextPageToken")
        if not token:
            return result

def show_summary():
    print("="*60)
    print(f"   🎉 루덴스 축제 부스 운영 결산 리포트 ({PROJECT_ID}) 🎉")
    print("="*60)

    # 1. GameState/stats 조회
    stats_url = f"{BASE_URL}/GameState/stats"
    stats_doc = make_request(stats_url)
    if stats_doc and "fields" in stats_doc:
        f = stats_doc["fields"]
        revenue = f.get("totalRevenue", {}).get("integerValue", "0")
        plays = f.get("totalPlays", {}).get("integerValue", "0")
        successes = f.get("totalSuccesses", {}).get("integerValue", "0")
        dolls = f.get("totalDolls", {}).get("integerValue", "미확인")
        legendary = f.get("totalLegendaryDolls", {}).get("integerValue", "미확인")
        reg = f.get("totalRegistrations", {}).get("integerValue", "0")

        print(f"💰 총 누적 매출: {int(revenue):,} 원")
        print(f"🎮 총 게임 플레이 수: {plays} 회")
        print(f"🏆 총 인형/상품 당첨 수: {successes} 회")
        print(f"🧸 남은 인형 재고: {dolls} 개")
        print(f"✨ 남은 레전드 인형 재고: {legendary} 개")
        print(f"📝 총 참가자 등록 수: {reg} 명")
    else:
        print("[-] GameState/stats 문서를 불러올 수 없습니다.")

    # 2. Participants 집계
    docs = list_all_documents("Participants")
    if docs:
        male_cnt = 0
        female_cnt = 0
        picked_cnt = 0

        for doc in docs:
            fields = doc.get("fields", {})
            g = fields.get("gender", {}).get("stringValue", "")
            is_picked = fields.get("isPicked", {}).get("booleanValue", False)

            if "남" in g: male_cnt += 1
            elif "여" in g: female_cnt += 1

            if is_picked: picked_cnt += 1

        print("\n--- 👫 소개팅 매칭 현황 ---")
        print(f"총 참가자: {len(docs)}명 (남: {male_cnt}명, 여: {female_cnt}명)")
        print(f"매칭 성사(뽑힘): {picked_cnt}명 / 미뽑힘: {len(docs) - picked_cnt}명")

    # 3. RhythmLeaderboard 확인
    rhythm_data = list_all_documents("RhythmLeaderboard")
    if rhythm_data:
        scores = []
        for d in rhythm_data:
            f = d.get("fields", {})
            title = f.get("songTitle", {}).get("stringValue", "-")
            acc = f.get("accuracy", {}).get("doubleValue", 0.0)
            combo = f.get("maxCombo", {}).get("integerValue", "0")
            scores.append({"title": title, "accuracy": float(acc), "combo": int(combo)})

        scores.sort(key=lambda x: x["accuracy"], reverse=True)
        print("\n--- 🎵 커플 리듬게임 명예의 전당 Top 5 ---")
        for i, s in enumerate(scores[:5], 1):
            print(f" {i}위: {s['title']} - 정확도 {s['accuracy']:.1f}% (Max Combo: {s['combo']})")

    print("="*60)

def purge_personal_data():
    confirm = input("\n⚠️ 정말로 모든 참가자 인스타그램 및 개인정보를 삭제하시겠습니까? (yes/no): ")
    if confirm.strip().lower() != "yes":
        print("[*] 개인정보 삭제가 취소되었습니다.")
        return

    for collection in ("Participants", "ParticipantKeys", "ProfileClaims", "MatchResults", "GameRounds"):
        docs = list_all_documents(collection)
        print(f"[*] {collection}: {len(docs)}건 삭제 중...")
        for doc in docs:
            doc_path = doc["name"]
            del_url = f"https://firestore.googleapis.com/v1/{doc_path}"
            if make_request(del_url, method="DELETE") is None:
                raise RuntimeError(f"삭제 실패: {doc_path}")

    print("[+] 모든 참가자 개인정보가 안전하게 파기되었습니다.")

if __name__ == "__main__":
    show_summary()
    if "--purge" in sys.argv:
        purge_personal_data()
