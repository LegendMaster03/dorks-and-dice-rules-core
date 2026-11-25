// Project supplementary SRD definitions without treating every action candidate as a core action.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function correctLegacyCombatOption(r){
 if(r.id==="attack-option.grapple"){
  r.entries[0]=r.entries[0].replace("The target of your grapple must be no more than one size larger than you, and it must be within your reach.","The target of your grapple must be no more than one size larger than you and must be within your reach.");
  r.entries[1]=r.entries[1].replace("a grapple check,","a grapple check instead of an attack roll:").replace(" You succeed automatically if the target is {@condition incapacitated}.","").replace("(see the appendix)","(see appendix PH-A)");
  return "Restore publisher grapple contest instead of an attack roll and remove later automatic-success errata (p.95).";
 }
 if(r.id==="attack-option.shove"){
  r.entries[1]=r.entries[1].replace("The target of your shove must be no more than one size larger than you, and it must be within your reach. You make","The target must be no more than one size larger than you and must be within your reach. Instead of making an attack roll, you make").replace(" You succeed automatically if the target is {@condition incapacitated}.","").replace("If you succeed,","If you win the contest,");
  return "Restore publisher shove contest instead of an attack roll and remove later automatic-success errata (p.96).";
 }
 if(r.id==="reaction-option.opportunity-attack"){
  r.entries[0]="In a fight, everyone is constantly watching for a chance to strike an enemy who is fleeing or passing by. Such a strike is called an opportunity attack.";
  r.entries[1]=r.entries[1].replace("The attack interrupts the provoking creature's movement, occurring right before","The attack occurs right before");
  return "Restore published opportunity attack wording (p.95).";
 }
}

function generate(input,manifest,src,pages,tag){
 const modern=tag==="52",edition="SRD"+tag,slug=s=>s.toLowerCase().normalize("NFKC").replace(/[’']/g,"").replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const norm=s=>s.toLowerCase().replace(/[’‘]/g,"'").replace(/[\u00ad\u2010-\u2014]/g,"-").replace(/\s+/g," ");
 const records=[],report={edition:manifest.officialPageImport.release,sourceCounts:{},nativeCounts:{},excluded:[],corrections:[],pendingReferences:[],locatorLimitations:[]};
 const pageByNumber=new Map(pages.map(p=>[p.page,p]));
 const omit=new Set(["name","source","page","reprintedAs","basicRules2024","hasFluff","hasFluffImages","ruleType","colStyles"]);
 const supplementary=modern?{"Don or Doff a Shield":["equipment-option",[92],"Shield (Utilize Action to Don or Doff)"],"End Concentration":["activity",[179],"end Concentration"],"Escape a Grapple":["combat-option",[182],"Ending a Grapple"],"Opportunity Attack":["reaction-option",[185],"Opportunity Attacks"],"Two-Weapon Fighting":["combat-option",[89],"Light"]}:{"Activate an Item":["equipment-option",[206],"Activating an Item"],"Don or Doff a Shield":["equipment-option",[64],"Donning and Doffing Armor"],"End Concentration":["activity",[102],"end concentration"],"Escape a Grapple":["combat-option",[95],"Escaping a Grapple"],"Grapple":["attack-option",[95,96],"Grappling"],"Opportunity Attack":["reaction-option",[95],"Opportunity Attacks"],"Other Activity":["activity",[91],"Other Activity on Your Turn"],"Shove":["attack-option",[96],"Shoving a Creature"],"Stabilize a Creature":["combat-option",[98],"Stabilizing a Creature"],"Two-Weapon Fighting":["combat-option",[95],"Two-Weapon Fighting"]};
 const legacyTrap={"Collapsing Roof":[196],"Falling Net":[196],"Fire-Breathing Statue":[197],"Pits":[197,198],"Poison Darts":[197],"Poison Needle":[198],"Rolling Sphere":[198],"Sphere of Annihilation":[198]};
 const modernTrap={"Collapsing Roof":[199],"Falling Net":[199],"Fire-Casting Statue":[199,200],"Hidden Pit":[200],"Poisoned Darts":[200],"Poisoned Needle":[200,201],"Rolling Stone":[201],"Spiked Pit":[201]};
 const tableLoc={"Damage Types":[modern?180:97],"Getting Into and Out of Armor; Donning and Doffing Armor":[64],"Magic Item Values by Rarity; Magic Item Rarities and Values":[206],"Scribing Spell Scrolls; Spell Scroll Costs":[103]};
 const core=new Set(modern?["Attack","Dash","Disengage","Dodge","Help","Hide","Influence","Magic","Ready","Search","Study","Utilize"]:["Attack","Cast a Spell","Dash","Disengage","Dodge","Help","Hide","Ready","Search","Use an Item"]);
 for(const [file,array] of [["variantrules","variantrule"],["conditionsdiseases","disease"],["conditionsdiseases","status"],["trapshazards","trap"],["trapshazards","hazard"],["tables","table"],["objects","object"],["actions","action"],["vehicles","vehicle"],["deities","deity"]]){
 for(const [i,c] of input[file][array].entries()){
 if(c.source!==edition)continue;
 report.sourceCounts[array]=(report.sourceCounts[array]??0)+1;
 const pointer={path:file+".json",pointer:"/"+array+"/"+i};
 const exclude=reason=>report.excluded.push({...pointer,name:c.name,reason});
 if(array==="vehicle"){exclude("Apparatus of the Crab is an existing magic-item identity; vehicle-specific projection needs separate reconciliation.");continue;}
 if(array==="deity"){exclude("Publisher pantheon table provenance not established; retain in reference corpus only.");continue;}
 if(array==="action"&&core.has(c.name)){exclude("Already promoted in core actions.");continue;}
 let kind=array,name=c.name,locs=[],needle=c.name;
 if(array==="variantrule"){
 kind=modern?"glossary":"rule";
 if(modern){
 const candidates=pages.filter(p=>p.page>=176&&p.page<=191&&norm(p.textLayout).includes(norm(c.name)));
 const heading=candidates.find(p=>p.textLayout.split("\n").some(line=>line.trim().split(/\s{2,}/).some(cell=>norm(cell.trim())===norm(c.name))));
 const candidate=heading??candidates.find(p=>p.page===c.page)??candidates[0];
 if(!candidate)throw Error("Missing glossary locator "+name);
 locs=[candidate.page];if(!heading)report.locatorLimitations.push({name,reason:"Literal name locator only, not isolated heading."});
 }else{locs=[201,202];}
 }else if(array==="disease"){kind=modern?"magical-contagion":"disease";locs=modern?[194]:c.name==="Sight Rot"?[199,200]:[199];}
 else if(array==="status"){locs=modern?[{"Bloodied":177,"Concentration":179,"Surprised":189}[name]]:[name==="Concentration"?102:90];if(name==="Surprised")needle="Surprise";}
 else if(array==="trap"){locs=(modern?modernTrap:legacyTrap)[name];}
 else if(array==="hazard"){locs=[c.page];kind=c.page===195?"environmental-effect":"hazard";}
 else if(array==="table"){locs=tableLoc[name];name=name.split("; ").at(-1);needle=name;}
 else if(array==="object"){kind="rule";name="Breaking Objects";needle="Statistics for Objects";locs=[203];}
 else if(array==="action"){const spec=supplementary[name];if(!spec)throw Error("Unclassified action "+name);[kind,locs,needle]=spec;}
 if(!locs?.length||locs.some(n=>!pageByNumber.has(n)))throw Error("Missing page "+name+" "+locs);
 if(!locs.some(n=>norm(pageByNumber.get(n).textLayout).includes(norm(needle))))throw Error("Missing publisher locator "+name+" "+needle+" "+locs);
 const r={id:kind+"."+slug(name),kind,name};
 for(const [k,v] of Object.entries(c))if(!omit.has(k))r[k]=JSON.parse(JSON.stringify(v));
 if(modern&&kind==="glossary"&&name==="Player Character"){
 if(r.entries?.length!==1||!r.entries[0].includes("{@link player|https://coveryourtracks.eff.org}")||!pageByNumber.get(186).textLayout.includes("player. See also “Character Creation.”"))throw Error("Unexpected Player Character source");
 r.entries=["A player character is a character controlled by a player. See also “Character Creation.”"];
 report.corrections.push({id:r.id,reason:"Remove non-publisher hyperlink and restore the printed Character Creation cross-reference on SRD 5.2.1 p.186."});
 }
 if(array==="object"){for(const k of ["size","objectType","ac","hp","immune"])delete r[k];report.corrections.push({id:r.id,reason:"Publisher general object-damage rules, not a generic creature-like stat block; remove renderer-only envelope."});}
 if(!modern&&name==="Madness"){
 const going=r.entries.find(x=>x?.name==="Going Mad");
 if(!going||!going.entries[1].includes("Sanity"))throw Error("Unexpected Madness candidate");
 going.entries[0]=going.entries[0].replace("the spell effects in the {@book Player's Handbook|SRD51}","the spell effects of those spells");
 going.entries[1]="Resisting a madness-inducing effect usually requires a Wisdom or Charisma saving throw.";
 report.corrections.push({id:r.id,reason:"Publisher pp.201–202 omit the Sanity ability-score variant and use the spells themselves, not a Player's Handbook pointer."});
 }
 if(array==="action"){
 if(kind==="attack-option"){delete r.time;r.replacesAttack=true;report.corrections.push({id:r.id,reason:"Legacy grapple/shove replaces an attack within the Attack action, rather than requiring a separate full action."});}
 if(!modern){const reason=correctLegacyCombatOption(r);if(reason)report.corrections.push({id:r.id,reason});}
 if(modern&&name==="Don or Doff a Shield"){r.actionId="action.utilize";r.entries=["A {@item Shield|srd52} can be donned or doffed using the {@action Utilize|srd52} action."];report.corrections.push({id:r.id,reason:"Publisher p.92 explicitly specifies Utilize action."});}
 }
 r.catalogReferences=locs.map(n=>pageByNumber.get(n).id);
 if(r.replacesAttack)r.catalogReferences.push("action.attack");
 if(r.actionId)r.catalogReferences.push(r.actionId);
 if(modern&&kind==="glossary"&&name==="Ability Check")r.catalogReferences.push("rule.ability-check");
 if(modern&&kind==="glossary"&&name==="Save")r.catalogReferences.push("glossary.saving-throw");
 r.source={release:manifest.officialPageImport.release,url:pageByNumber.get(locs[0]).source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:locs,candidatePage:c.page??null,structuredSource:{provider:src.provider,commit:src.commit,...pointer},structuredStatus:"pinned edition-specific projection; publisher locator verified; full body and semantic review pending"};
 const {source,catalogReferences,...body}=r;
 for(const reference of new Set([...JSON.stringify(body).matchAll(/\{@(spell|creature|condition|skill|item|feat|action|sense|variantrule|language|class|race|optfeature|classFeature|subclassFeature|status|table) ([^}]+)\}/g)].map(m=>m[1]+"|"+m[2])))report.pendingReferences.push({id:r.id,reference,reason:"semantic reference reconciliation pending"});
 records.push(r);report.nativeCounts[kind]=(report.nativeCounts[kind]??0)+1;
 }}
 const ids=new Set(records.map(r=>r.id));if(ids.size!==records.length)throw Error("Duplicate supplementary identity");
 const allowed=new Set([...ids,...pages.map(p=>p.id),"rule.ability-check",...Array.from(core,n=>"action."+slug(n==="Use an Item"?"Use an Object":n))]);
 for(const r of records)for(const id of r.catalogReferences)if(!allowed.has(id))throw Error("Dangling "+id);
 report.nativeCount=records.length;return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=x=>JSON.stringify(x,null,2)+"\n";
const input=Object.fromEntries(["variantrules","conditionsdiseases","trapshazards","tables","objects","actions","vehicles","deities"].map(n=>[n,read("data/reference/srd/structured/"+n+".json")]));
const src=read("data/reference/srd/structured-manifest.json"),check=process.argv.includes("--check");
for(const [pack,tag,files] of [["dnd-5e","51",["053-077","078-102","178-202","203-227"]],["dnd-5-5e","52",["080-104","155-179","180-204","205-229"]]]){
 const base="data/rulesets/"+pack+"/",manifest=read(base+"manifest.json"),pages=files.flatMap(n=>read(base+"native/rulebook/pages-"+n+".json")).sort((a,b)=>a.page-b.page);
 const {records,report}=generate(input,manifest,src,pages,tag);
 for(const [file,body] of [[base+"native/supplementary-rules.json",json(records)],["data/reference/srd/supplementary-promotion-"+tag+".json",json(report)]]){
  const target=path.join(root,file);
  if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale supplementary promotion "+file);}
  else fs.writeFileSync(target,body);
 }
 console.log(pack+": "+report.nativeCount+" supplementary rules");
}
