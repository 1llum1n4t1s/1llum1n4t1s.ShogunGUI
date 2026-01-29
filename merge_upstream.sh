#!/bin/bash
# フォーク元 (upstream) の main を取り込み、upstream 所有ファイルは常に本家版で上書きする。
# 競合を避けるため FORK_POLICY.md / upstream_owned.txt を参照すること。

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if ! git remote get-url upstream &>/dev/null; then
    echo "upstream リモートが未設定です。追加してください:"
    echo "  git remote add upstream https://github.com/yohey-w/multi-agent-shogun.git"
    exit 1
fi

LIST_FILE="upstream_owned.txt"
if [ ! -f "$LIST_FILE" ]; then
    echo "upstream_owned.txt が見つかりません。" >&2
    exit 1
fi

echo "Fetching upstream..."
git fetch upstream

echo "Merging upstream/main..."
if ! git merge upstream/main -m "Merge upstream/main"; then
    echo "Merge で競合が発生しました。upstream 所有ファイルを本家版で上書きします..."
    while IFS= read -r path || [ -n "$path" ]; do
        path=$(echo "$path" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
        [ -z "$path" ] && continue
        if git ls-tree -r --name-only upstream/main | grep -q "^${path}$"; then
            git checkout upstream/main -- "$path" 2>/dev/null || true
        fi
    done < "$LIST_FILE"
    git add -A
    git commit -m "Merge upstream/main; restore upstream-owned files"
    echo "競合を解消し、upstream 所有ファイルを本家版で復元してコミットしました。"
    echo "Done."
    exit 0
fi

echo "Upstream 所有ファイルを本家版に揃えます..."
while IFS= read -r path || [ -n "$path" ]; do
    path=$(echo "$path" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
    [ -z "$path" ] && continue
    if git ls-tree -r --name-only upstream/main 2>/dev/null | grep -q "^${path}$"; then
        git checkout upstream/main -- "$path" 2>/dev/null || true
    fi
done < "$LIST_FILE"
git add -A
if git diff --cached --quiet; then
    echo "upstream 所有ファイルは既に本家版と一致しています。"
else
    git commit -m "Restore upstream-owned files after merge"
    echo "upstream 所有ファイルを本家版で復元してコミットしました。"
fi
echo "Done."
