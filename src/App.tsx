import React, { useState, useEffect, useRef } from 'react';
import { 
  Compass, 
  MapPin, 
  Search, 
  MessageSquare, 
  Heart, 
  Save, 
  Info, 
  AlertTriangle,
  ChevronRight,
  Filter,
  Navigation,
  Wind,
  Sun,
  X,
  Send
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { trails, Trail } from './data/trails';
import { chatWithAI } from './lib/gemini';

interface Message {
  id: string;
  role: 'user' | 'model';
  content: string;
  type: 'text' | 'function';
  functionCall?: any;
}

export default function App() {
  const [messages, setMessages] = useState<Message[]>([
    { 
      id: '1', 
      role: 'model', 
      content: "Hello! I'm your TrailScout AI assistant. I can help you find hiking trails, save your favorites, and plan your next adventure. Where are you looking to hike today?",
      type: 'text'
    }
  ]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [savedTrailIds, setSavedTrailIds] = useState<string[]>([]);
  const [activeTab, setActiveTab] = useState<'explore' | 'favorites'>('explore');
  const [nearbyTrails, setNearbyTrails] = useState<Trail[]>(trails.slice(0, 4));
  const chatEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetchSavedTrails();
    scrollToBottom();
  }, [messages]);

  const scrollToBottom = () => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const fetchSavedTrails = async () => {
    try {
      const res = await fetch('/api/saved-trails');
      const data = await res.json();
      setSavedTrailIds(data.savedTrailIds || []);
    } catch (e) {
      console.error("Failed to fetch saved trails", e);
    }
  };

  const saveTrail = async (trailId: string) => {
    try {
      const res = await fetch('/api/save-trail', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ trailId })
      });
      const data = await res.json();
      setSavedTrailIds(data.savedTrailIds);
    } catch (e) {
      console.error("Failed to save trail", e);
    }
  };

  const removeTrail = async (trailId: string) => {
    try {
      const res = await fetch(`/api/saved-trails/${trailId}`, {
        method: 'DELETE'
      });
      const data = await res.json();
      setSavedTrailIds(data.savedTrailIds);
    } catch (e) {
      console.error("Failed to remove trail", e);
    }
  };

  const handleSendMessage = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!inputValue.trim() || isLoading) return;

    const userMsg: Message = { id: Date.now().toString(), role: 'user', content: inputValue, type: 'text' };
    setMessages(prev => [...prev, userMsg]);
    setInputValue('');
    setIsLoading(true);

    const history = messages.map(m => ({
      role: m.role,
      parts: [{ text: m.content }]
    }));

    const aiResponse = await chatWithAI(inputValue, history);
    setIsLoading(false);

    if (typeof aiResponse === 'string') {
      const errorMsg: Message = { id: (Date.now() + 1).toString(), role: 'model', content: aiResponse, type: 'text' };
      setMessages(prev => [...prev, errorMsg]);
    } else if (aiResponse.type === 'function') {
      const funcMsg: Message = { 
        id: (Date.now() + 1).toString(), 
        role: 'model', 
        content: `Executing ${aiResponse.function}...`, 
        type: 'function',
        functionCall: aiResponse
      };
      setMessages(prev => [...prev, funcMsg]);
      handleFunctionCall(aiResponse);
    } else {
      const textMsg: Message = { id: (Date.now() + 1).toString(), role: 'model', content: aiResponse.text!, type: 'text' };
      setMessages(prev => [...prev, textMsg]);
    }
  };

  const handleFunctionCall = async (call: any) => {
    const { function: funcName, parameters } = call;

    if (funcName === 'saveTrail') {
      const trail = trails.find(t => t.name.toLowerCase().includes(parameters.trailName.toLowerCase()));
      if (trail) {
        await saveTrail(trail.id);
        setMessages(prev => [...prev, { 
          id: Date.now().toString(), 
          role: 'model', 
          type: 'text', 
          content: `Sure! I've saved "${trail.name}" to your favorites.` 
        }]);
      } else {
        setMessages(prev => [...prev, { 
          id: Date.now().toString(), 
          role: 'model', 
          type: 'text', 
          content: `I couldn't find a trail named "${parameters.trailName}" to save.` 
        }]);
      }
    } else if (funcName === 'getSavedTrails') {
      const savedTrails = trails.filter(t => savedTrailIds.includes(t.id));
      const content = savedTrails.length > 0 
        ? `You have ${savedTrails.length} saved trails: ${savedTrails.map(t => t.name).join(', ')}.`
        : "You haven't saved any trails yet.";
      setMessages(prev => [...prev, { id: Date.now().toString(), role: 'model', type: 'text', content }]);
    } else if (funcName === 'getTrails') {
      // Mock filtering for RAG demo
      let filtered = trails;
      if (parameters.difficulty) {
        filtered = filtered.filter(t => t.difficulty === parameters.difficulty);
      }
      if (parameters.maxDistanceMiles) {
        filtered = filtered.filter(t => t.distanceMiles <= parameters.maxDistanceMiles);
      }
      setNearbyTrails(filtered.slice(0, 5));
      setMessages(prev => [...prev, { 
        id: Date.now().toString(), 
        role: 'model', 
        type: 'text', 
        content: `I've found ${filtered.length} trails that match your criteria in ${parameters.location || 'your area'}. You can see them in the Explore panel.` 
      }]);
    }
  };

  return (
    <div className="flex h-screen bg-[#F2F4EF] font-sans text-[#1B2610] overflow-hidden">
      {/* Sidebar Navigation */}
      <aside className="hidden lg:flex w-64 bg-[#2D3A1A] text-[#F2F4EF] flex-col transition-all">
        <div className="p-8">
          <div className="flex items-center gap-3 mb-10">
            <div className="w-10 h-10 bg-[#6B8E23] rounded-xl flex items-center justify-center">
              <Compass className="h-6 w-6 text-white" />
            </div>
            <h1 className="text-xl font-semibold italic font-serif">SummitScout</h1>
          </div>
          
          <nav className="space-y-6">
            <div className="space-y-2">
              <p className="text-[10px] uppercase tracking-widest opacity-50 font-bold mb-4">Explorer</p>
              <button 
                onClick={() => setActiveTab('explore')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-colors ${activeTab === 'explore' ? 'bg-[#6B8E23] text-white' : 'hover:bg-white/5 opacity-70'}`}
              >
                <Navigation className="w-5 h-5 opacity-80" />
                <span className="text-sm font-medium">Trail Assistant</span>
              </button>
              <button 
                onClick={() => setActiveTab('favorites')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-colors ${activeTab === 'favorites' ? 'bg-[#6B8E23] text-white' : 'hover:bg-white/5 opacity-70'}`}
              >
                <Heart className={`w-5 h-5 ${savedTrailIds.length > 0 ? 'fill-white/80' : ''}`} />
                <span className="text-sm font-medium">Saved Hikes</span>
              </button>
            </div>
          </nav>
        </div>
        
        <div className="mt-auto p-6 border-t border-white/10">
          <div className="bg-[#3D4C27] rounded-2xl p-4">
            <div className="flex justify-between items-center mb-2">
              <span className="text-[10px] font-bold uppercase tracking-wider opacity-60">Local Weather</span>
              <span className="text-xs font-semibold">72°F</span>
            </div>
            <p className="text-[11px] leading-relaxed opacity-90">Sunny skies in Yosemite Village. Perfect for hiking.</p>
          </div>
        </div>
      </aside>

      {/* Main Chat Area */}
      <main className="flex-1 flex flex-col relative border-r border-[#D8DECF]">
        <header className="h-20 border-b border-[#D8DECF] flex items-center justify-between px-8 bg-white/50 backdrop-blur-sm z-10">
          <div>
            <h2 className="font-serif text-2xl font-bold">Hiking Assistant</h2>
            <p className="text-xs text-[#5D6B4E]">Online • Ready to find your next adventure</p>
          </div>
          <div className="flex items-center gap-4">
            <button 
              onClick={() => setMessages([messages[0]])}
              className="px-4 py-2 border border-[#D8DECF] rounded-full text-[10px] uppercase font-bold tracking-tight hover:bg-white transition-colors"
            >
              Clear History
            </button>
            <div className="flex lg:hidden items-center gap-2">
               <button onClick={() => setActiveTab(activeTab === 'explore' ? 'favorites' : 'explore')} className="p-2 bg-[#E4E9DC] rounded-xl text-[#4A5D23]">
                 {activeTab === 'explore' ? <Heart className="w-5 h-5" /> : <Navigation className="w-5 h-5" />}
               </button>
            </div>
          </div>
        </header>

        <div className="flex-1 overflow-y-auto p-8 space-y-8 scroll-smooth">
          <AnimatePresence initial={false}>
            {messages.map((m) => (
              <motion.div
                key={m.id}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className={`flex gap-4 ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}
              >
                {m.role === 'model' && (
                  <div className="w-8 h-8 rounded-lg bg-[#6B8E23] flex-shrink-0 flex items-center justify-center text-white font-bold text-xs">S</div>
                )}
                
                <div className={`max-w-md ${
                  m.role === 'user' 
                    ? 'bg-[#2D3A1A] text-[#F2F4EF] p-4 rounded-2xl rounded-tr-none shadow-sm' 
                    : 'space-y-4 w-full'
                }`}>
                  {m.role === 'model' ? (
                    <div className="bg-white p-5 rounded-2xl rounded-tl-none shadow-sm border border-[#E1E6D9]">
                      {m.type === 'function' ? (
                        <div className="flex items-center gap-2 text-xs font-mono text-[#5D6B4E] bg-[#F9FAF7] p-2 rounded-lg border border-[#E1E6D9]">
                          <Navigation className="w-3 h-3 animate-pulse" />
                          {m.content}
                        </div>
                      ) : (
                        <div className="text-sm leading-relaxed whitespace-pre-wrap">
                          {m.content}
                          {m.content.toLowerCase().includes('recommend') && (
                            <div className="mt-4 text-xs italic text-[#5D6B4E] border-l-2 border-[#6B8E23] pl-3 py-1 bg-[#F9FAF7]">
                              Recommendation note: Be sure to bring at least 2L of water; the mid-day sun can be intense.
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  ) : (
                    <p className="text-sm leading-relaxed">{m.content}</p>
                  )}
                </div>
              </motion.div>
            ))}
            {isLoading && (
              <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="flex gap-4 justify-start">
                <div className="w-8 h-8 rounded-lg bg-[#6B8E23] flex-shrink-0 animate-pulse"></div>
                <div className="bg-white p-5 rounded-2xl rounded-tl-none shadow-sm border border-[#E1E6D9] w-20 flex justify-center">
                   <div className="flex gap-1">
                    <div className="w-1.5 h-1.5 bg-[#6B8E23] rounded-full animate-bounce"></div>
                    <div className="w-1.5 h-1.5 bg-[#6B8E23] rounded-full animate-bounce delay-75"></div>
                    <div className="w-1.5 h-1.5 bg-[#6B8E23] rounded-full animate-bounce delay-150"></div>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
          <div ref={chatEndRef} />
        </div>

        {/* Input Bar */}
        <div className="p-8 bg-gradient-to-t from-[#F2F4EF] via-[#F2F4EF] to-transparent">
          <form onSubmit={handleSendMessage} className="max-w-3xl mx-auto space-y-4">
            <div className="flex items-center gap-4 bg-white p-2 pl-6 rounded-2xl border border-[#D8DECF] shadow-lg transition-shadow focus-within:shadow-xl">
              <input
                type="text"
                placeholder="Ask about trails, gear, or safety..."
                className="flex-1 text-sm outline-none bg-transparent"
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                disabled={isLoading}
              />
              <button 
                type="submit"
                disabled={!inputValue.trim() || isLoading}
                className="w-12 h-12 bg-[#2D3A1A] text-white rounded-xl flex items-center justify-center hover:bg-[#1B2610] transition-colors disabled:opacity-50"
              >
                <Send className="w-5 h-5" />
              </button>
            </div>
            <div className="flex justify-center flex-wrap gap-3">
              <span className="text-[10px] font-bold text-[#5D6B4E] uppercase tracking-widest">Quick Actions:</span>
              <button type="button" onClick={() => setInputValue('Recommend an easy hike with views')} className="text-[10px] uppercase font-bold text-[#6B8E23] hover:underline underline-offset-4 decoration-2">Easy with views</button>
              <span className="text-[#D8DECF] font-bold">•</span>
              <button type="button" onClick={() => setInputValue('Find hard trails with waterfalls')} className="text-[10px] uppercase font-bold text-[#6B8E23] hover:underline underline-offset-4 decoration-2">Difficulty Filter</button>
              <span className="text-[#D8DECF] font-bold">•</span>
              <button type="button" onClick={() => setInputValue('What are some safety tips for hiking Yosemite?')} className="text-[10px] uppercase font-bold text-[#6B8E23] hover:underline underline-offset-4 decoration-2">Safety Tips</button>
            </div>
          </form>
        </div>
      </main>

      {/* Right Action Panel (Trails) */}
      <aside className="hidden xl:flex w-80 bg-white p-8 flex-col overflow-y-auto">
        <h3 className="font-serif text-xl font-bold mb-6">{activeTab === 'explore' ? 'Recommended Trails' : 'Your Favorites'}</h3>
        
        <div className="space-y-8">
          {/* Stats context */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1">
              <p className="text-[10px] font-bold text-[#5D6B4E] uppercase tracking-tighter">Estimated Pace</p>
              <div className="flex items-end gap-1">
                <span className="text-3xl font-light font-serif">3.2</span>
                <span className="text-[10px] font-bold mb-1 uppercase tracking-widest text-[#9E9E9E]">mph</span>
              </div>
            </div>
            <div className="space-y-1">
              <p className="text-[10px] font-bold text-[#5D6B4E] uppercase tracking-tighter">Trail Count</p>
              <div className="flex items-end gap-1">
                <span className="text-3xl font-light font-serif">{(activeTab === 'explore' ? nearbyTrails : trails.filter(t => savedTrailIds.includes(t.id))).length}</span>
                <span className="text-[10px] font-bold mb-1 uppercase tracking-widest text-[#9E9E9E]">hikes</span>
              </div>
            </div>
          </div>

          <div className="p-4 bg-[#F2F4EF] rounded-2xl border border-[#D8DECF]">
            <p className="text-[10px] font-bold text-[#5D6B4E] uppercase mb-3 tracking-widest">Safety Check</p>
            <div className="space-y-2">
              <div className="flex items-center gap-3">
                <div className="w-2 h-2 rounded-full bg-green-500 animate-pulse"></div>
                <span className="text-[11px] font-semibold">Low fire risk today</span>
              </div>
              <div className="flex items-center gap-3">
                <div className="w-2 h-2 rounded-full bg-orange-400"></div>
                <span className="text-[11px] font-semibold">High UV Index (8)</span>
              </div>
            </div>
          </div>

          <div className="space-y-4 pt-4 border-t border-[#F2F4EF]">
            <p className="text-[10px] font-bold text-[#5D6B4E] uppercase tracking-widest mb-4">
              {activeTab === 'explore' ? 'Featured Suggestions' : 'Saved for Later'}
            </p>
            
            <div className="space-y-4">
              {(activeTab === 'explore' ? nearbyTrails : trails.filter(t => savedTrailIds.includes(t.id))).map((trail) => (
                <motion.div 
                  layout
                  key={trail.id}
                  className="group bg-white rounded-2xl p-4 border border-[#E1E6D9] shadow-sm hover:border-[#6B8E23] transition-all cursor-pointer relative overflow-hidden"
                >
                  <div className="flex justify-between items-start mb-2">
                    <h4 className="font-bold text-xs truncate max-w-[120px]">{trail.name}</h4>
                    <span className={`text-[9px] px-2 py-0.5 rounded font-black uppercase tracking-tighter ${
                      trail.difficulty === 'easy' ? 'bg-[#E4E9DC] text-[#4A5D23]' :
                      trail.difficulty === 'moderate' ? 'bg-[#FFF8E6] text-[#B45F06]' :
                      'bg-red-50 text-red-700'
                    }`}>
                      {trail.difficulty}
                    </span>
                  </div>
                  <p className="text-[10px] text-[#5D6B4E] mb-3">{trail.distanceMiles} miles • {trail.features[0]}</p>
                  <div className="flex gap-2">
                    <button 
                      onClick={() => savedTrailIds.includes(trail.id) ? removeTrail(trail.id) : saveTrail(trail.id)}
                      className="flex-1 py-2 bg-[#F2F4EF] rounded-xl text-[10px] font-bold text-[#4A5D23] hover:bg-[#6B8E23] hover:text-white transition-colors"
                    >
                      {savedTrailIds.includes(trail.id) ? 'Saved' : 'Save to Favorites'}
                    </button>
                    <button className="p-2 bg-[#F2F4EF] rounded-xl hover:bg-[#D8DECF] transition-colors">
                      <ChevronRight className="w-3 h-3" />
                    </button>
                  </div>
                </motion.div>
              ))}
              {(activeTab === 'explore' ? nearbyTrails : trails.filter(t => savedTrailIds.includes(t.id))).length === 0 && (
                <div className="py-10 text-center opacity-50 space-y-2">
                  <Search className="w-8 h-8 mx-auto text-[#D8DECF]" />
                  <p className="text-xs font-medium">No trails to display</p>
                </div>
              )}
            </div>
          </div>
        </div>
      </aside>

      {/* Safety Alert Flash */}
      <AnimatePresence>
        {messages.some(m => m.content.toLowerCase().includes('hard') || m.content.toLowerCase().includes('dangerous')) && (
          <motion.div 
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.9 }}
            className="fixed bottom-24 right-8 max-w-xs z-50"
          >
            <div className="bg-[#FFF8E6] border border-[#FFD966] p-5 rounded-2xl flex gap-3 shadow-2xl backdrop-blur-md">
              <AlertTriangle className="text-[#B45F06] w-6 h-6 flex-shrink-0" />
              <div>
                <p className="text-[#B45F06] font-bold text-xs uppercase tracking-widest mb-1">Safety Alert</p>
                <p className="text-[#B45F06] text-[11px] leading-snug opacity-90">Challenging terrain detected. Ensure you have offline maps and a headlamp.</p>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

