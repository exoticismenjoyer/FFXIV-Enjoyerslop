FFXIV StatusPulse is a Dalamud plugin that syncs your character's real-time status (Name, Job, World, Location, and Duty) to a Supabase database every 60 seconds.

To install, copy the URL below and add it to your Custom Plugin Repositories in /xlsettings (Experimental tab):
https://raw.githubusercontent.com/YOUR_USERNAME/FFXIVStatusPulse/main/enjoyer.json

Setup Supabase
Go to your Supabase Dashboard, open the SQL Editor, paste the code below, and click Run. This creates the required table:

create table player_status (
  name text primary key,
  job text,
  world text,
  territory text,
  in_duty boolean,
  duty_name text,
  timestamp timestamptz default now()
);

Once installed, use the command /pulse to enter your Supabase Project URL and API Key.The plugin will automatically update your character's row in the player_status table whenever you are logged in.
