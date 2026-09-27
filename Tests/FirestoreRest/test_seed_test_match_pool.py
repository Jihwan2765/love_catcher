"""테스트 시드의 기존 데이터 보존과 재실행 동작을 네트워크 없이 검사한다."""

import importlib.util
from pathlib import Path
from unittest.mock import patch


source = Path(__file__).resolve().parents[2] / "admin_tools" / "seed_test_match_pool_cloud_shell.py"
spec = importlib.util.spec_from_file_location("seed_match_pool", source)
seed = importlib.util.module_from_spec(spec)
spec.loader.exec_module(seed)

docs = {}
registration_count = 10


def get_document(_project, _token, path):
    return docs.get(path)


def call(_project, _token, method, suffix, payload=None):
    global registration_count
    assert method == "POST" and suffix == ":commit"
    writes = payload["writes"]
    assert len(writes) == 3
    assert all(item["currentDocument"]["exists"] is False for item in writes[:2])
    assert writes[2]["currentDocument"]["exists"] is True
    assert writes[2]["transform"]["fieldTransforms"] == [
        {"fieldPath": "totalRegistrations", "increment": {"integerValue": "1"}}]
    assert not any("totalDolls" in str(item) or "totalLegendaryDolls" in str(item) for item in writes)
    for item in writes[:2]:
        name = item["update"]["name"]
        path = name.split("/documents/", 1)[1]
        assert path not in docs
        docs[path] = {"fields": item["update"]["fields"]}
    registration_count += 1
    return 200, {}


profile = next(seed.profiles(60))
with patch.object(seed, "get_document", get_document), patch.object(seed, "call", call):
    assert seed.write_profile("ludens-booth26-2", "unused", profile) == "created"
    assert registration_count == 11
    docs["Participants/" + profile["id"]]["fields"]["isPicked"] = {"booleanValue": True}
    assert seed.write_profile("ludens-booth26-2", "unused", profile) == "existing"
    assert registration_count == 11
    assert docs["Participants/" + profile["id"]]["fields"]["isPicked"]["booleanValue"] is True
    docs["Participants/" + profile["id"]]["fields"]["isTestData"] = {"booleanValue": False}
    try:
        seed.write_profile("ludens-booth26-2", "unused", profile)
    except RuntimeError:
        pass
    else:
        raise AssertionError("기존 참가자를 덮어쓰려고 했습니다")

print("PASS: 테스트 시드 원자적 등록, 재실행, 기존 프로필 보존")
