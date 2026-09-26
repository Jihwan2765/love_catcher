#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
[루덴스 축제 부스] 초기 참가자 시드 데이터 주입 스크립트
행사 첫날 시작 시, 참가자가 한 명도 없어서 이성 매칭이 불가능한 상황을 방지하기 위해
지인/운영진용 초기 프로필 20명(남 10명, 여 10명)을 Firestore에 등록합니다.
"""

import sys
import json
import urllib.request
import urllib.error
from _auth import authorization_header

# Windows 콘솔 UTF-8 출력 보장
try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

# 설정: Firebase 프로젝트 ID
PROJECT_ID = "ludens-booth26-2"

# 인자값으로 프로젝트 ID가 넘어오면 우선 사용 (예: python seed_dummy_participants.py my-project-id)
if len(sys.argv) > 1 and sys.argv[1].strip():
    PROJECT_ID = sys.argv[1].strip()

BASE_URL = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents"

DUMMY_USERS = [
    # 남성 프로필 (10명)
    {"name": "민우", "insta": "minwoo_dev", "gender": "남", "bio": "게임 개발이랑 밴드 음악 좋아해요!"},
    {"name": "준혁", "insta": "june_hyeok99", "gender": "남", "bio": "오늘 축제에서 맛있는 거 같이 먹을 분!"},
    {"name": "도현", "insta": "dohyun_photo", "gender": "남", "bio": "사진 찍는 거 좋아합니다. 인생샷 찍어드려요"},
    {"name": "승원", "insta": "sw_gym_life", "gender": "남", "bio": "운동 좋아하고 유쾌한 성격입니다 :)"},
    {"name": "태윤", "insta": "taeyoon_cafe", "gender": "남", "bio": "예쁜 카페 투어 좋아하시는 분 환영해요"},
    {"name": "시우", "insta": "siwoo_cat", "gender": "남", "bio": "고양이 집사입니다! 냥이 이야기해요"},
    {"name": "진우", "insta": "jinwoo_game", "gender": "남", "bio": "루덴스 게임 다 깨고 가겠습니다"},
    {"name": "하준", "insta": "ha_june_v", "gender": "남", "bio": "영화 보는 거 좋아하시는 분 친해져요"},
    {"name": "재민", "insta": "jm_sketch", "gender": "남", "bio": "그림 그리고 전시회 보러 다니는 거 좋아해요"},
    {"name": "현우", "insta": "hyeon_guitar", "gender": "남", "bio": "통기타 치는 공대생입니다"},

    # 여성 프로필 (10명)
    {"name": "서연", "insta": "seoyeon_dessert", "gender": "여", "bio": "달달한 디저트 카페 좋아하시는 분!"},
    {"name": "지민", "insta": "jimin_daily", "gender": "여", "bio": "ENFP 텐션 맞춰주실 분 구해요 ㅎㅎ"},
    {"name": "유진", "insta": "yujin_travel", "gender": "여", "bio": "여행이랑 맛집 탐방 좋아하는 대학생입니다"},
    {"name": "채원", "insta": "chaewon_doggy", "gender": "여", "bio": "강아지 산책 같이 시켜요!"},
    {"name": "다은", "insta": "daeun_flower", "gender": "여", "bio": "날씨 좋은데 축제 같이 구경할 분~"},
    {"name": "수아", "insta": "sua_music", "gender": "여", "bio": "노래방 자주 가시는 분 환영해요"},
    {"name": "예린", "insta": "yerin_art", "gender": "여", "bio": "디자인 전공입니다! 감성 통하는 분 좋아요"},
    {"name": "하은", "insta": "haeun_climb", "gender": "여", "bio": "클라이밍이랑 러닝 좋아하는 운동러입니다"},
    {"name": "소율", "insta": "soyul_movie", "gender": "여", "bio": "넷플릭스 영화 추천해주실 분!"},
    {"name": "민서", "insta": "minseo_study", "gender": "여", "bio": "시험 끝나고 신나게 놀 사람 찾아요"}
]

def make_request(url, method="GET", data=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", authorization_header(PROJECT_ID))
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

def init_game_stats():
    print(f"\n[*] 부스 초기 통계(GameState/stats) 설정 중...")
    mask = "updateMask.fieldPaths=totalDolls&updateMask.fieldPaths=totalLegendaryDolls&updateMask.fieldPaths=totalRevenue&updateMask.fieldPaths=totalRegistrations&updateMask.fieldPaths=totalPlays&updateMask.fieldPaths=totalSuccesses"
    url = f"{BASE_URL}/GameState/stats?{mask}"
    payload = {
        "fields": {
            "totalDolls": {"integerValue": "100"},
            "totalLegendaryDolls": {"integerValue": "10"},
            "totalRevenue": {"integerValue": "0"},
            "totalRegistrations": {"integerValue": str(len(DUMMY_USERS))},
            "totalPlays": {"integerValue": "0"},
            "totalSuccesses": {"integerValue": "0"}
        }
    }
    res = make_request(url, method="PATCH", data=payload)
    if res:
        print(f"[+] 통계 세팅 완료 (인형 100개, 레전더리 10개, 초기 등록자 {len(DUMMY_USERS)}명)")
    else:
        print(f"[-] 통계 세팅 실패")

def seed_users():
    import time
    print(f"\n[*] 초기 참가자 {len(DUMMY_USERS)}명 주입 시작 (프로젝트: {PROJECT_ID})...")
    success_count = 0
    for idx, u in enumerate(DUMMY_USERS, 1):
        time.sleep(0.2)
        doc_id = u['insta']
        url = f"{BASE_URL}/Participants?documentId={doc_id}"
        payload = {
            "fields": {
                "name": {"stringValue": u["name"]},
                "insta": {"stringValue": u["insta"]},
                "bio": {"stringValue": u["bio"]},
                "gender": {"stringValue": u["gender"]},
                "isPicked": {"booleanValue": False},
                "attempts": {"integerValue": "0"}
            }
        }
        res = make_request(url, method="POST", data=payload)
        if res:
            success_count += 1
            print(f" [{idx}/{len(DUMMY_USERS)}] 등록 성공: {u['name']} (@{u['insta']}, {u['gender']})")
        else:
            print(f" [{idx}/{len(DUMMY_USERS)}] 등록 실패: {u['name']}")

    print(f"\n[+] 주입 완료! (성공: {success_count}/{len(DUMMY_USERS)})")

if __name__ == "__main__":
    print("이 예전 시드 도구는 ParticipantKeys와 원자적 등록 집계를 만들지 않습니다.")
    print("운영 DB에서는 실행하지 마세요. 테스트 참가자는 게임의 등록 화면에서 추가하세요.")
    sys.exit(2)
