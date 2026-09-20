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
        dolls = f.get("totalDolls", {}).get("integerValue", "100")
        reg = f.get("totalRegistrations", {}).get("integerValue", "0")

        print(f"💰 총 누적 매출: {int(revenue):,} 원")
        print(f"🎮 총 게임 플레이 수: {plays} 회")
        print(f"🏆 총 인형/상품 당첨 수: {successes} 회")
        print(f"🧸 남은 인형 재고: {dolls} 개")
        print(f"📝 총 참가자 등록 수: {reg} 명")
    else:
        print("[-] GameState/stats 문서를 불러올 수 없습니다.")

    # 2. Participants 집계
    part_url = f"{BASE_URL}/Participants?pageSize=300"
    part_data = make_request(part_url)
    if part_data and "documents" in part_data:
        docs = part_data["documents"]
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
    rhythm_url = f"{BASE_URL}/RhythmLeaderboard?pageSize=50"
    rhythm_data = make_request(rhythm_url)
    if rhythm_data and "documents" in rhythm_data:
        scores = []
        for d in rhythm_data["documents"]:
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

    part_url = f"{BASE_URL}/Participants?pageSize=300"
    part_data = make_request(part_url)
    if not part_data or "documents" not in part_data:
        print("[-] 삭제할 참가자 데이터가 없습니다.")
        return

    docs = part_data["documents"]
    print(f"[*] {len(docs)}명의 개인정보 삭제 시작...")
    for doc in docs:
        doc_path = doc["name"]
        del_url = f"https://firestore.googleapis.com/v1/{doc_path}"
        make_request(del_url, method="DELETE")

    print("[+] 모든 참가자 개인정보가 안전하게 파기되었습니다.")

if __name__ == "__main__":
    show_summary()
    if "--purge" in sys.argv:
        purge_personal_data()
