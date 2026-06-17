
Just a personal collection of Dalamud plugins I made for myself. Add the repo URL below to your Custom Plugin Repositories in `/xlsettings` under the Experimental tab and you're good to go.

```
https://raw.githubusercontent.com/exoticismenjoyer/FFXIV-Enjoyerslop/master/enjoyer.json
```

---

**StatusPulse**

Syncs your character's status (Name, Job, World, Location, Duty) to a Supabase database every 60 seconds. I built this for my personal blog to show what I'm up to in-game. Use `/pulse` to open the config and enter your Supabase URL and API key.

You'll need to create the table in Supabase first. Open the SQL Editor in your dashboard and run this:

```sql
create table player_status (
  name text primary key,
  job text,
  world text,
  territory text,
  in_duty boolean,
  duty_name text,
  timestamp timestamptz default now()
);
```

---

**EmoteMirror**

Mirrors emotes back at whoever uses them on you. Someone waves at you, you wave back automatically. Use `/emotemirror` to configure it.

The emote hook is based on the approach used in [Right Back At You](https://github.com/dodingdaga/DalamudPlugins) by DodingDaga. Go check that out if you want the original.




---
