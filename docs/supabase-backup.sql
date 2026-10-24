-- TheCafePOS: off-site backups in Supabase Storage.
-- Run once in the Supabase dashboard: SQL Editor > New query > paste > Run. Safe to run again.

-- Private bucket: files are never served publicly.
insert into storage.buckets (id, name, public, file_size_limit)
values ('backups', 'backups', false, 52428800) -- 50 MB per file
on conflict (id) do update set public = false;

-- The POS uses the publishable (anon) key, which anyone with the till could copy.
-- It may only ADD new files to this bucket: no list, read, overwrite or delete,
-- so a leaked key cannot expose or wipe the shop's backups.
drop policy if exists "TheCafePOS tills upload backups" on storage.objects;
create policy "TheCafePOS tills upload backups"
on storage.objects for insert
to anon
with check (bucket_id = 'backups');

-- Restoring: Storage > backups > <machine name> > download a .db file,
-- then in the POS: Quản trị > Khôi phục từ bản sao lưu… and pick that file.
