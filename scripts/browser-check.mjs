import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';
const base=(process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
await fs.mkdir('test-results',{recursive:true});
const report={base,checks:[],errors:[],console:[]};
const browser=await chromium.launch({headless:true,args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--disable-dev-shm-usage']});
const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});
const page=await context.newPage();
page.on('pageerror',e=>report.errors.push(e.message));
page.on('console',m=>{if(m.type()==='error'||m.type()==='warning')report.console.push(m.text());});
const state=()=>page.evaluate(()=>globalThis.__textSpaceState);
async function until(test,message,timeout=30000){const end=Date.now()+timeout;while(Date.now()<end){if(await test())return;await page.waitForTimeout(150);}throw new Error(message);}
async function click(name){let c;await until(async()=>{c=await page.evaluate(name=>(globalThis.__textSpaceState?.controls||[]).find(c=>(c.name===name||c.command===name)&&c.enabled&&c.width>0&&c.height>0&&c.x+c.width/2>=0&&c.y+c.height/2>=0&&c.x+c.width/2<innerWidth&&c.y+c.height/2<innerHeight),name);return !!c;},'Missing control '+name);await page.mouse.click(c.x+c.width/2,c.y+c.height/2);await page.waitForTimeout(250);}
async function boot(){await page.goto(base+'?test=1',{waitUntil:'domcontentloaded'});await page.waitForFunction(()=>globalThis.__textSpaceState?.ready||globalThis.__textSpaceError,null,{timeout:180000});assert.equal(await page.evaluate(()=>globalThis.__textSpaceError??null),null);await until(async()=>(await state())?.canvas.width>200,'Editor did not lay out');}
async function check(name,action){await action();report.checks.push(name);console.log('PASS',name);}
try{
 await check('publication provenance',async()=>{let info;await until(async()=>{try{const r=await fetch(base+'build-info.json?t='+Date.now());if(!r.ok)return false;info=await r.json();return !process.env.TEXTSPACE_EXPECTED_COMMIT||info.commit===process.env.TEXTSPACE_EXPECTED_COMMIT;}catch{return false;}},'Build metadata unavailable or outdated',90000);assert.equal(info.host,'Uno WebAssembly');report.build=info;});
 await check('Uno application startup',async()=>{await boot();const s=await state();assert.equal(s.runtime,'Uno WebAssembly / Skia');assert.ok(s.words>100);assert.ok(s.pages>0);await page.screenshot({path:'test-results/desktop.png'});});
 await check('ribbon interaction',async()=>{await click('Insert tab');assert.equal((await state()).selectedTab,'Insert');await click('Home tab');});
 await check('new document and typing',async()=>{await click('File tab');await click('Blank document template');await until(async()=>(await state()).text==='','Blank template did not load');const {canvas:c}=await state();await page.mouse.click(c.x+c.paperLeft+75*c.scale,c.y+18+75*c.scale-c.scrollY);await page.keyboard.insertText('Hello TextSpace');await until(async()=>(await state()).text==='Hello TextSpace','Typing failed');});
 await check('formatting and history',async()=>{await page.keyboard.press('Control+a');await page.keyboard.press('Control+b');await until(async()=>(await state()).style.bold,'Bold failed');await page.keyboard.press('Control+End');await page.keyboard.press('Enter');await page.keyboard.insertText('Second paragraph');await until(async()=>(await state()).text.endsWith('Second paragraph'),'Paragraph failed');await page.keyboard.press('Control+z');await until(async()=>!(await state()).text.includes('Second paragraph'),'Undo failed');await page.keyboard.press('Control+y');await until(async()=>(await state()).text.endsWith('Second paragraph'),'Redo failed');});
 await check('native download',async()=>{const pending=page.waitForEvent('download');await page.keyboard.press('Control+s');const d=await pending;await d.saveAs('test-results/document.textspace');const model=JSON.parse(await fs.readFile('test-results/document.textspace','utf8'));assert.equal(model.formatVersion,1);assert.ok(model.blocks.length>=2);});
 await check('recovery after reload',async()=>{const text=(await state()).text;await page.waitForTimeout(2000);await boot();assert.equal((await state()).text,text);});
 await check('DOCX export',async()=>{await click('File tab');await click('File Export');const pending=page.waitForEvent('download');await click('Word document');const d=await pending;await d.saveAs('test-results/document.docx');const data=await fs.readFile('test-results/document.docx');assert.equal(data.subarray(0,2).toString(),'PK');await click('Back to document');});
 await check('compact layout',async()=>{await page.setViewportSize({width:1000,height:760});await page.waitForTimeout(600);await page.screenshot({path:'test-results/compact.png'});assert.ok((await state()).canvas.width>300);});
 assert.deepEqual(report.errors,[]);report.success=true;
}catch(e){report.success=false;report.failure=String(e.stack||e);report.state=await state().catch(()=>null);process.exitCode=1;console.error(e);await page.screenshot({path:'test-results/failure.png'}).catch(()=>{});
}finally{await fs.writeFile('test-results/report.json',JSON.stringify(report,null,2));await browser.close();}
