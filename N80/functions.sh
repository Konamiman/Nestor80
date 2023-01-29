#!/bin/sh

output() {
	TPUTARGS=""
	[ -n "$GITHUB_ACTIONS" ] && TPUTARGS="-T xterm-256color"
	echo "$(tput ${TPUTARGS} setaf "$1")$2$(tput ${TPUTARGS} sgr0)"
}

banner() {
	echo
	output 6 "----- $1 -----"
	echo
}
