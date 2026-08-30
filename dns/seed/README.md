# Seed copies of the DNSCrypt server list

These are the DNSCrypt project's public server list and relay list with their
minisign signatures, exactly as published upstream. dnscrypt-proxy verifies the
signatures before using either file.

They are here, tracked, rather than only in `dns/` because `dns/*.md` is the
working copy that dnscrypt-proxy rewrites whenever it refreshes the list — which
would mean a permanently dirty working tree. The release archive is built from
this folder, so it carries the list wherever it is packaged, including from a
fresh clone.

Shipping them matters. Without a list on disk, dnscrypt-proxy has to download one
the first time it runs, and to do that it resolves a host through the provider's
plain DNS — which, on exactly the networks that need encrypted DNS, is the thing
returning forged answers. The resolver then never becomes usable.

To refresh them, copy the current `dns/public-resolvers.md`, `dns/relays.md` and
their `.minisig` files over these once dnscrypt-proxy has fetched a newer list.
